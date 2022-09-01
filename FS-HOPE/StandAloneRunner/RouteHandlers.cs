﻿using System;
using System.Collections.Generic;
using System.Net;

using Newtonsoft.Json;

using Clifton.Core.ExtensionMethods;
using Clifton.Core.Semantics;
using Clifton.Core.Services.SemanticProcessorService;

using FlowSharpHopeCommon;

namespace StandAloneRunner
{
    public class RouteHandlers : BaseRouteHandlers
    {
        protected const string INSTANTIATE_RECEPTOR = "instantiateReceptor";
        protected const string INSTANTIATE_SEMANTIC_TYPE = "instantiateSemanticType";
        protected const string PUBLISH_SEMANTIC_TYPE = "publishSemanticType";
        protected const string ENABLE_DISABLE_RECEPTOR = "enableDisableReceptor";

        protected SemanticProcessor sp;

        // TODO: Membrane needs to be added to the type name as well.
        protected Dictionary<string, IReceptor> receptors = new Dictionary<string, IReceptor>();

        public RouteHandlers(SemanticProcessor sp)
        {
            routes = new Dictionary<string, Func<HttpListenerContext, string, (string text, string mime)>>()
            {
                {INSTANTIATE_RECEPTOR, InstantiateReceptor},
                {INSTANTIATE_SEMANTIC_TYPE, InstantiateSemanticType },
                {PUBLISH_SEMANTIC_TYPE, PublishSemanticType },
                {ENABLE_DISABLE_RECEPTOR, EnableDisableReceptor },
            };

            this.sp = sp;
        }

        protected (string text, string mime) InstantiateReceptor(HttpListenerContext context, string data)
        {
            var typeName = data.RightOf('=');
            InstantiateReceptor(typeName);
            return ("OK", "text/text");
        }

        protected (string text, string mime) InstantiateSemanticType(HttpListenerContext context, string data)
        {
            var t = Type.GetType("App." + data.RightOf('=') + ", HelloWorld");
            var st = (ISemanticType)Activator.CreateInstance(t);
            var json = JsonConvert.SerializeObject(st);
            return (json, "text/json");
        }

        protected (string text, string mime) PublishSemanticType(HttpListenerContext context, string data)
        {
            // TODO: Fix assumptions about ordering of params
            var parms = data.Split('&');
            var t = Type.GetType("App." + parms[0].RightOf('=') + ", HelloWorld");
            var st = (ISemanticType)JsonConvert.DeserializeObject(parms[1].RightOf('='), t);
            sp.ProcessInstance<HopeRunner.HopeMembrane>(st);
            return ("OK", "text/text");
        }

        protected (string text, string mime) EnableDisableReceptor(HttpListenerContext context, string data)
        {
            // TODO: Fix assumptions about ordering of params
            var parms = data.Split('&');
            var typeName = "App." + parms[0].RightOf('=');

            if (parms[1].RightOf('=').To_b())
            {
                // enable
                if (!receptors.ContainsKey(typeName))
                {
                    InstantiateReceptor(typeName);
                }
            }
            else
            {
                // disable
                if (!receptors.TryGetValue(typeName, out var receptor)) return ("OK", "text/text");
                sp.Unregister<HopeRunner.HopeMembrane>(receptor);
                receptors.Remove(typeName);
            }

            return ("OK", "text/text");
        }

        protected void InstantiateReceptor(string typeName)
        {
            Program.form.BeginInvoke(() =>
            {
                var t = Type.GetType(typeName + ", HelloWorld");
                var receptor = (IReceptor)Activator.CreateInstance(t);
                sp.Register<HopeRunner.HopeMembrane>(receptor);
                receptors[typeName] = receptor;
            });
        }
    }
}
