/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;

using WebSocketSharp;

using FlowSharpLib;

namespace FlowSharpClient
{
    public class MyListener { }

    public static partial class WebSocketHelpers
    {
        private static WebSocket ws;

        // cmd=CmdUpdateProperty&Name=btnTest&PropertyName=Text&Value=Foobar
        public static void UpdateProperty(string name, string propertyName, string value)
        {
            Connect();
            ws.Send($"cmd=CmdUpdateProperty&Name={name}&PropertyName={propertyName}&Value={value}");
        }

        public static void ClearCanvas()
        {
            Connect();
            ws.Send("cmd=CmdClearCanvas");
        }

        public static void DropShape(string shapeName, string name, int x, int y, string text = "")
        {
            Connect();
            ws.Send($"cmd=CmdDropShape&ShapeName={shapeName}&Name={x}&X={y}&Y={text}&Text={name}");
        }

        public static void DropShape(string shapeName, string name, Rectangle r, string text = "")
        {
            Connect();
            ws.Send($"cmd=CmdDropShape&ShapeName={shapeName}&Name={name}&X={r.X}&Y={r.Y}&Width={r.Width}&Height={r.Height}&Text={text}");
        }

        public static void DropShape(string shapeName, string name, Rectangle r, Color fillColor, string text = "")
        {
            Connect();
            ws.Send($"cmd=CmdDropShape&ShapeName={shapeName}&Name={name}&X={r.X}&Y={r.Y}&Width={r.Width}&Height={r.Height}&Text={text}&FillColor={fillColor.ToHtmlColor('!')}");
        }

        public static void DropShape(string shapeName, string name, int x, int y, int w, int h, string text = "")
        {
            Connect();
            ws.Send($"cmd=CmdDropShape&ShapeName={shapeName}&Name={name}&X={x}&Y={y}&Width={w}&Height={h}&Text={text}");
        }

        public static void DropConnector(string shapeName, string name, int x1, int y1, int x2, int y2)
        {
            Connect();
            ws.Send($"cmd=CmdDropConnector&ConnectorName={shapeName}&Name={name}&X1={x1}&Y1={y1}&X2={x2}&Y2={y2}");
        }

        public static void DropConnector(string shapeName, string name, int x1, int y1, int x2, int y2, Color borderColor)
        {
            Connect();
            ws.Send($"cmd=CmdDropConnector&ConnectorName={shapeName}&Name={name}&X1={x1}&Y1={y1}&X2={x2}&Y2={y2}&BorderColor={borderColor.ToHtmlColor('!')}");
        }

        private static void Connect()
        {
            if (ws?.IsAlive == true) return;
            // ws = new WebSocket("ws://192.168.1.165:1100/flowsharp", new MyListener());
            var localip = GetLocalHostIPs()[0].ToString();
            ws = new WebSocket("ws://" + localip + ":1100/flowsharp", new MyListener());

            ws.OnMessage += (sender, e) =>
            {
                var response = e.Data;
            };

            ws.Connect();
        }

        private static List<IPAddress> GetLocalHostIPs()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ret = host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToList();

            return ret;
        }
    }
}