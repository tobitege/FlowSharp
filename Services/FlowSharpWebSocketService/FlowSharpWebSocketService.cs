/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Clifton.Core.ModuleManagement;
using Clifton.Core.Semantics;
using Clifton.Core.ServiceManagement;

using FlowSharpServiceInterfaces;

namespace FlowSharpWebSocketService
{
    public class FlowSharpWebSocketModule : IModule
    {
        public void InitializeServices(IServiceManager serviceManager)
        {
            serviceManager.RegisterSingleton<IFlowSharpWebSocketService, FlowSharpWebSocketService>();
        }
    }

    public class FlowSharpWebSocketService : ServiceBase, IFlowSharpWebSocketService
    {
        protected HttpListener listener;
        protected CancellationTokenSource cancellationTokenSource;
        protected Task acceptLoopTask;

        public override void FinishedInitialization()
        {
            base.FinishedInitialization();
            ServiceManager.Get<ISemanticProcessor>().Register<FlowSharpMembrane, WebSocketSender>();
            StartServer();
        }

        public void StartServer()
        {
            if (listener != null)
            {
                return;
            }

            var port = RuntimeControlConfiguration.GetWebSocketPort();
            HttpListenerException lastListenerException = null;
            var prefixes = GetServerPrefixes(port);

            foreach (var prefix in prefixes)
            {
                var candidateListener = new HttpListener();
                candidateListener.Prefixes.Add(prefix);

                try
                {
                    candidateListener.Start();
                    listener = candidateListener;
                    break;
                }
                catch (HttpListenerException ex)
                {
                    lastListenerException = ex;
                    candidateListener.Close();
                }
            }

            if (listener == null)
            {
                throw lastListenerException ?? new HttpListenerException();
            }

            cancellationTokenSource = new CancellationTokenSource();
            acceptLoopTask = Task.Run(() => AcceptLoop(cancellationTokenSource.Token), cancellationTokenSource.Token);
        }

        public void StopServer()
        {
            if (listener == null)
            {
                return;
            }

            cancellationTokenSource.Cancel();
            listener.Stop();
            listener.Close();
            listener = null;
            acceptLoopTask = null;
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }

        protected List<IPAddress> GetLocalHostIPs()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host?.AddressList?.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToList();
        }

        protected List<string> GetServerPrefixes(int port)
        {
            var prefixes = new List<string>();
            var ips = GetLocalHostIPs() ?? new List<IPAddress>();
            prefixes.AddRange(ips.Select(ip => $"http://{ip}:{port}/flowsharp/"));
            prefixes.Add($"http://localhost:{port}/flowsharp/");
            prefixes.Add($"http://127.0.0.1:{port}/flowsharp/");

            return prefixes
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        protected void AcceptLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;

                try
                {
                    context = listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                Task.Run(() => HandleConnection(context, cancellationToken), cancellationToken);
            }
        }

        protected void HandleConnection(HttpListenerContext context, CancellationToken cancellationToken)
        {
            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                return;
            }

            HttpListenerWebSocketContext webSocketContext = context.AcceptWebSocketAsync(null).GetAwaiter().GetResult();
            WebSocket socket = webSocketContext.WebSocket;

            try
            {
                while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    string msg = ReceiveTextMessage(socket, cancellationToken);
                    if (msg == null)
                    {
                        break;
                    }

                    var data = ParseMessage(msg);
                    var jsonResp = PublishSemanticMessage(data);
                    if (!string.IsNullOrEmpty(jsonResp))
                    {
                        SendTextMessage(socket, jsonResp, cancellationToken);
                    }
                }
            }
            finally
            {
                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                {
                    socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).GetAwaiter().GetResult();
                }

                socket.Dispose();
            }
        }

        protected string ReceiveTextMessage(WebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            var segment = new ArraySegment<byte>(buffer);
            var sb = new StringBuilder();

            while (true)
            {
                var result = socket.ReceiveAsync(segment, cancellationToken).GetAwaiter().GetResult();

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    return null;
                }

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    break;
                }
            }

            return sb.ToString();
        }

        protected void SendTextMessage(WebSocket socket, string data, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            var segment = new ArraySegment<byte>(bytes);
            socket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken).GetAwaiter().GetResult();
        }

        protected Dictionary<string, string> ParseMessage(string msg)
        {
            var data = new Dictionary<string, string>();
            var dataPackets = msg.Split('&');

            foreach (var dp in dataPackets)
            {
                if (string.IsNullOrWhiteSpace(dp))
                {
                    continue;
                }

                var varValue = dp.Split('=', 2);
                if (varValue.Length == 0)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(varValue[0]);
                var value = varValue.Length == 2 ? varValue[1] : string.Empty;
                data[key] = value;
            }

            return data;
        }

        protected string PublishSemanticMessage(Dictionary<string, string> data)
        {
            if (!data.TryGetValue("cmd", out var cmdName))
            {
                return JsonConvert.SerializeObject(new { error = "Missing command." });
            }

            var t = SemanticTypeParser.NewCommand(cmdName);
            if (t == null)
            {
                return JsonConvert.SerializeObject(new { error = "Unknown command.", command = cmdName });
            }

            try
            {
                PopulateType(t, data);
                ServiceManager.Get<ISemanticProcessor>().ProcessInstance<FlowSharpMembrane>(t, true);
                return t is IHasResponse response ? response.SerializeResponse() ?? string.Empty : string.Empty;
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { error = ex.GetBaseException().Message, command = t.GetType().Name });
            }
        }

        protected void PopulateType(ISemanticType packet, Dictionary<string, string> data)
        {
            var values = SemanticTypeParser.ToDictionary(data);
            SemanticTypeParser.PopulateType(packet, values);
        }
    }
}
