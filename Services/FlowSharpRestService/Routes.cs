﻿/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

using Clifton.Core.ExtensionMethods;
using Clifton.Core.Semantics;
using Clifton.Core.ServiceManagement;

using FlowSharpServiceInterfaces;

namespace FlowSharpRestService
{
    public class Route
    {
        public string Verb { get; set; }
        public string Path { get; set; }
    }

    public partial class Routes
    {
        protected Dictionary<Route, Action<HttpListenerContext, string>> routes;
        protected IServiceManager serviceManager;
        private const string OK = "OK";

        public Routes(IServiceManager serviceManager)
        {
            routes = new Dictionary<Route, Action<HttpListenerContext, string>>();
            this.serviceManager = serviceManager;
        }

        public void Route(HttpListenerContext context, string data)
        {
            Console.Write(context.Request.HttpMethod.ToUpper() + " ");
            Console.WriteLine(context.Request.RawUrl.LeftOf("?").RightOf("/").ToLower());
            var route = routes.SingleOrDefault(kvp => kvp.Key.Verb == context.Request.HttpMethod.ToUpper() &&
                                                      kvp.Key.Path == context.Request.RawUrl.LeftOf("?").RightOf("/").ToLower());
            route.Value?.Invoke(context, data);
        }

        public void InitializeRoutes()
        {
            routes[new Route() { Verb = "GET", Path = "flowsharp" }] = PublishSemanticMessage;
        }

        protected void PublishSemanticMessage(HttpListenerContext context, string data)
        {
            var resp = OK;
            var nvc = context.Request.QueryString;
            var stname = nvc["cmd"];
            var st = Type.GetType("FlowSharpServiceInterfaces." + stname + ",FlowSharpServiceInterfaces");
            var t = Activator.CreateInstance(st) as ISemanticType;
            PopulateType(t, nvc);
            // Synchronous, because however we're processing the command doesn't know (or need to know) that it's
            // coming from an HTTP GET, but we don't want to issue the response until the action has been performed.
            serviceManager.Get<ISemanticProcessor>().ProcessInstance<FlowSharpMembrane>(t, true);
            if (t is IHasResponse hr)
            {
                resp = hr.SerializeResponse();
            }
            Response(context, resp, resp == OK ? "text/plain" : "application/json");
        }

        protected void PopulateType(ISemanticType packet, NameValueCollection nvc)
        {
            foreach (var key in nvc.AllKeys)
            {
                var pi = packet.GetType().GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (pi == null) continue;
                object valOfType = null;
                var ptype = pi.PropertyType;

                if (ptype.IsGenericType)
                {
                    // We assume it's a nullable type
                    ptype = ptype.GenericTypeArguments[0];
                }

                valOfType = Convert.ChangeType(Uri.UnescapeDataString(nvc[key].Replace('+', ' ')), ptype);
                pi.SetValue(packet, valOfType);
            }
        }

        public void Response(HttpListenerContext context, string resp, string contentType)
        {
            var utf8data = Encoding.UTF8.GetBytes(resp);
            context.Response.ContentType = contentType;
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = utf8data.Length;
            context.Response.OutputStream.Write(utf8data, 0, utf8data.Length);
        }
    }
}
