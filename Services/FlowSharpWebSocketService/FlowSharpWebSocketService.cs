﻿/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

using WebSocketSharp;
using WebSocketSharp.Server;

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
        protected WebSocketServer wss;

        public override void FinishedInitialization()
        {
            base.FinishedInitialization();
            ServiceManager.Get<ISemanticProcessor>().Register<FlowSharpMembrane, WebSocketSender>();
            StartServer();
        }

        public void StartServer()
        {
            var ips = GetLocalHostIPs();
            var address = ips[0].ToString();
            var port = 1100;
            var ipaddr = new IPAddress(address.Split('.').Select(a => Convert.ToByte(a)).ToArray());
            wss = new WebSocketServer(ipaddr, port, null);
            wss.AddWebSocketService<Server>("/flowsharp");
            wss.Start();
        }

        public void StopServer()
        {
            wss.Stop();
        }

        protected List<IPAddress> GetLocalHostIPs()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host?.AddressList?.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToList();
        }
    }

    public class Server : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            if (e.Type != Opcode.Text) return;
            var msg = e.Data;
            var data = ParseMessage(msg);
            var jsonResp = PublishSemanticMessage(data);
            if (!string.IsNullOrEmpty(jsonResp))
            {
                Send(jsonResp);
            }
        }

        protected Dictionary<string, string> ParseMessage(string msg)
        {
            var data = new Dictionary<string, string>();
            var dataPackets = msg.Split('&');

            foreach (var dp in dataPackets)
            {
                var varValue = dp.Split('=');
                data[varValue[0]] = varValue[1];
            }
            return data;
        }

        protected string PublishSemanticMessage(Dictionary<string, string> data)
        {
            string ret = null;
            var st = Type.GetType("FlowSharpServiceInterfaces." + data["cmd"] + ",FlowSharpServiceInterfaces");
            var t = Activator.CreateInstance(st) as ISemanticType;
            PopulateType(t, data);
            // Synchronous, because however we're processing the commands in order, otherwise we lose the point of a web socket,
            // which keeps the messages in order.
            ServiceManager.Instance.Get<ISemanticProcessor>().ProcessInstance<FlowSharpMembrane>(t, true);

            if (t is IHasResponse response)
            {
                ret = response.SerializeResponse();
            }

            return ret;
        }

        protected void PopulateType(ISemanticType packet, Dictionary<string, string> data)
        {
            foreach (string key in data.Keys)
            {
                var pi = packet.GetType().GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (pi == null) continue;

                var ptype = pi.PropertyType;
                if (ptype.IsGenericType)
                {
                    // We assume it's a nullable type
                    ptype = ptype.GenericTypeArguments[0];
                }

                var valOfType = Convert.ChangeType(Uri.UnescapeDataString(data[key].Replace('+', ' ')), ptype);
                pi.SetValue(packet, valOfType);
            }
        }
    }
}
