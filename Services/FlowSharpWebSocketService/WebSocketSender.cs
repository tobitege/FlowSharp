/* 
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

using Clifton.Core.Semantics;

using FlowSharpServiceInterfaces;

namespace FlowSharpWebSocketService
{
    public class WebSocketSender : IReceptor
    {
        public static ClientWebSocket ws = null;

        public void Process(ISemanticProcessor proc, IMembrane membrane, WebSocketSend cmd)
        {
            EstablishConnection();
            Send(cmd.Data);
        }

        protected void EstablishConnection()
        {
            // TODO: Right now, we're assuming one web socket client.
            if (ws == null || ws.State != WebSocketState.Open)
            {
                ws?.Dispose();
                ws = new ClientWebSocket();
                ws.ConnectAsync(new Uri("ws://127.0.0.1:1101/flowsharpapp"), CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        protected void Send(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data ?? string.Empty);
            var segment = new ArraySegment<byte>(bytes);
            ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}