/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

using FlowSharpLib;

namespace FlowSharpClient
{
    public static partial class WebSocketHelpers
    {
        private static ClientWebSocket ws;

        // cmd=CmdUpdateProperty&Name=btnTest&PropertyName=Text&Value=Foobar
        public static void UpdateProperty(string name, string propertyName, string value)
        {
            Connect();
            Send($"cmd=CmdUpdateProperty&Name={name}&PropertyName={propertyName}&Value={value}");
        }

        public static void ClearCanvas()
        {
            Connect();
            Send("cmd=CmdClearCanvas");
        }

        public static void DropShape(string shapeName, string name, int x, int y, string text = "")
        {
            Connect();
            Send($"cmd=CmdDropShape&ShapeName={shapeName}&Name={x}&X={y}&Y={text}&Text={name}");
        }

        public static void DropShape(string shapeName, string name, Rectangle r, string text = "")
        {
            Connect();
            Send($"cmd=CmdDropShape&ShapeName={shapeName}&Name={name}&X={r.X}&Y={r.Y}&Width={r.Width}&Height={r.Height}&Text={text}");
        }

        public static void DropShape(string shapeName, string name, Rectangle r, Color fillColor, string text = "")
        {
            Connect();
            Send($"cmd=CmdDropShape&ShapeName={shapeName}&Name={name}&X={r.X}&Y={r.Y}&Width={r.Width}&Height={r.Height}&Text={text}&FillColor={fillColor.ToHtmlColor('!')}");
        }

        public static void DropShape(string shapeName, string name, int x, int y, int w, int h, string text = "")
        {
            Connect();
            Send($"cmd=CmdDropShape&ShapeName={shapeName}&Name={name}&X={x}&Y={y}&Width={w}&Height={h}&Text={text}");
        }

        public static void DropConnector(string shapeName, string name, int x1, int y1, int x2, int y2)
        {
            Connect();
            Send($"cmd=CmdDropConnector&ConnectorName={shapeName}&Name={name}&X1={x1}&Y1={y1}&X2={x2}&Y2={y2}");
        }

        public static void DropConnector(string shapeName, string name, int x1, int y1, int x2, int y2, Color borderColor)
        {
            Connect();
            Send($"cmd=CmdDropConnector&ConnectorName={shapeName}&Name={name}&X1={x1}&Y1={y1}&X2={x2}&Y2={y2}&BorderColor={borderColor.ToHtmlColor('!')}");
        }

        private static void Connect()
        {
            if (ws != null && ws.State == WebSocketState.Open) return;

            var localip = GetLocalHostIPs()[0].ToString();
            ws?.Dispose();
            ws = new ClientWebSocket();
            ws.ConnectAsync(new Uri("ws://" + localip + ":1100/flowsharp"), CancellationToken.None).GetAwaiter().GetResult();
        }

        private static void Send(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data ?? string.Empty);
            var segment = new ArraySegment<byte>(bytes);
            ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None).GetAwaiter().GetResult();
        }

        private static List<IPAddress> GetLocalHostIPs()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ret = host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToList();

            return ret;
        }
    }
}