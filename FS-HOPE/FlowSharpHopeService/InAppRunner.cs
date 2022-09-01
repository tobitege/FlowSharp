using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json;

using Clifton.Core.ExtensionMethods;
using Clifton.Core.Semantics;
using Clifton.Core.Services.SemanticProcessorService;

using FlowSharpHopeCommon;

namespace App
{
    public class HopeMembrane : Membrane { };
}

namespace FlowSharpHopeService
{
    /// <summary>
    /// For in-memory, no app-domain, testing.
    /// Incomplete implementation:
    /// Processing
    /// EnableDisableReceptor
    /// Unload can't do anything because this is an in-memory load, the assembly cannot be unloaded.
    /// </summary>
    public class InAppRunner : IRunner
    {
        public bool Loaded { get; protected set; }
        public event EventHandler<HopeRunnerAppDomainInterface.ProcessEventArgs> Processing;

        protected SemanticProcessor sp;
        protected Assembly assy;
        protected IMembrane membrane;

        public InAppRunner()
        {
            sp = new SemanticProcessor();
            // membrane = new HopeMembrane();
            // membrane = sp.RegisterMembrane<HopeMembrane>();
            // membrane = new App.HopeMembrane();
            membrane = sp.RegisterMembrane<App.HopeMembrane>();

            sp.Processing += ProcessingSemanticType;
        }

        private void ProcessingSemanticType(object sender, ProcessEventArgs args)
        {
            var stMsg = new HopeRunnerAppDomainInterface.ProcessEventArgs()
            {
                FromMembraneTypeName = args.FromMembrane?.GetType()?.FullName,
                FromReceptorTypeName = args.FromReceptor?.GetType()?.FullName,
                ToMembraneTypeName = args.ToMembrane.GetType().FullName,
                ToReceptorTypeName = args.ToReceptor.GetType().FullName,
                SemanticTypeTypeName = args.SemanticType.GetType().FullName,
            };

            Processing.Fire(this, stMsg);
        }

        public void Load(string dll)
        {
            assy = Assembly.LoadFrom(dll);
            var t = assy.GetTypes().Single(at => at.Name == "HopeMembrane");
            membrane = (IMembrane)Activator.CreateInstance(t);
            sp.RegisterMembrane(membrane);
            Loaded = true;
        }

        public void Unload()
        {
            Loaded = false;
        }

        public void EnableDisableReceptor(string typeName, bool state)
        {
        }

        public List<ReceptorDescription> DescribeReceptor(string typeName)
        {
            var descrList = new List<ReceptorDescription>();
            var rtype = assy.GetTypes().Single(at => at.Name == typeName);

            var mis = rtype.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == "Process");

            foreach(var mi in mis)
            {
                var descr = new ReceptorDescription
                {
                    ReceptorTypeName = typeName,
                    ReceivingSemanticType = mi.GetParameters()[2].ParameterType.Name
                };
                descrList.Add(descr);
                var attrs = mi.GetCustomAttributes().Where(attr => attr is PublishesAttribute).Cast<PublishesAttribute>();
                foreach (var attr in attrs)
                {
                    descr.Publishes.Add(attr.PublishesType.Name);
                }
            }
            return descrList;
        }

        public void InstantiateReceptor(string name)
        {
            var t = assy.GetTypes().Single(at => at.Name == name);
            var inst = (IReceptor)Activator.CreateInstance(t);
            // sp.Register(membrane, inst);
            sp.Register<App.HopeMembrane>(inst);
        }

        public object InstantiateSemanticType(string typeName)
        {
            var st = assy.GetTypes().Single(t => t.Name == typeName);
            return Activator.CreateInstance(st);
        }

        public PropertyContainer DescribeSemanticType(string typeName)
        {
            var t = assy.GetTypes().Single(at => at.Name == typeName);
            var pis = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var pc = new PropertyContainer();
            BuildTypes(pc, pis);
            return pc;
        }

        public void Publish(string _, object st)
        {
            sp.ProcessInstance(membrane, (ISemanticType)st, true);
        }

        public void Publish(string typeName, string json)
        {
            var t = assy.GetTypes().Single(at => at.Name == typeName);
            var st = (ISemanticType)JsonConvert.DeserializeObject(json, t);
            // sp.ProcessInstance(membrane, st, true);
            sp.ProcessInstance<App.HopeMembrane>(st, true);
        }

        protected void BuildTypes(PropertyContainer pc, PropertyInfo[] pis)
        {
            foreach (var pi in pis)
            {
                var pd = new PropertyData() { Name = pi.Name, TypeName = pi.PropertyType.FullName };
                var cat = pi.GetCustomAttribute<CategoryAttribute>();
                var desc = pi.GetCustomAttribute<DescriptionAttribute>();
                pd.Category = cat?.Category;
                pd.Description = desc?.Description;
                pc.Types.Add(pd);

                if ((!pi.PropertyType.IsValueType) && (pd.TypeName != "System.String"))
                {
                    var pisChild = pi.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    pd.ChildType = new PropertyContainer();
                    BuildTypes(pd.ChildType, pisChild);
                }
            }
        }
    }
}
