using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;

using Newtonsoft.Json;

using Clifton.Core.ExtensionMethods;
using Clifton.Core.Semantics;

using FlowSharpHopeCommon;
using HopeRunnerAppDomainInterface;

namespace FlowSharpHopeService
{
    // Must be derived from MarshalByRefObject so that the Processing event handler stays wired up to its handler on the callback from the app domain.
    [Serializable]
    public class AppDomainRunner : MarshalByRefObject, IRunner
    {
        // We need this attribute, otherwise the class HigherOrderProgrammingService needs to be marked serializable,
        // which if you do that, starts a horrid cascade of classes (like Clifton.Core to start with) that must be marked as serializable as well.
        // Gross.
        // So why does Runner need to be marked as Serializable, and how do we avoid that?
        // Because this Runner wires up an event in the AppDomain, which requires serialiation of this class.
        [field:NonSerialized]
        public event EventHandler<HopeRunnerAppDomainInterface.ProcessEventArgs> Processing;

        [NonSerialized]
        protected AppDomain appDomain;
        [NonSerialized]
        public IHopeRunner appDomainRunner;
        [NonSerialized]
        protected Assembly loadedAssembly;
        [NonSerialized]
        protected HopeAssemblyLoadContext loadContext;

        public bool Loaded => appDomainRunner != null;

        public AppDomainRunner()
        {
        }

        public void Load(string fullDllName)
        {
            if (appDomainRunner == null)
            {
                string assemblyPath = Path.GetFullPath(fullDllName);
                string dllName = Path.GetFileNameWithoutExtension(assemblyPath);
                appDomain = CreateAppDomain(dllName);
                appDomainRunner = InstantiateRunner(assemblyPath, appDomain);

                if (appDomain != AppDomain.CurrentDomain)
                {
                    appDomain.DomainUnload += AppDomainUnloading;
                }
            }
        }

        public void Unload()
        {
            if (appDomain != null && appDomain != AppDomain.CurrentDomain)
            {
                appDomain.DomainUnload -= AppDomainUnloading;
            }

            appDomain = null;
            appDomainRunner = null;
            loadedAssembly = null;

            if (loadContext != null)
            {
                // Clear field reference before waiting for unload; otherwise this instance itself
                // roots the collectible context during weak-reference polling.
                var contextToUnload = loadContext;
                loadContext = null;
                UnloadCollectibleContext(contextToUnload);
            }
        }

        public void InstantiateReceptor(string name)
        {
            appDomainRunner?.InstantiateReceptor(name);
        }

        public List<ReceptorDescription> DescribeReceptor(string name)
        {
            var descrList = new List<ReceptorDescription>();
            Type receptorType = ResolveType(name);

            if (receptorType == null)
            {
                return descrList;
            }

            var processMethods = receptorType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == "Process");

            foreach (var method in processMethods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length < 3)
                {
                    continue;
                }

                var descr = new ReceptorDescription
                {
                    ReceptorTypeName = receptorType.FullName ?? receptorType.Name,
                    ReceivingSemanticType = parameters[2].ParameterType.Name
                };

                descrList.Add(descr);

                var publishesAttrs = method.GetCustomAttributes()
                    .Where(attr => attr is PublishesAttribute)
                    .Cast<PublishesAttribute>();

                foreach (var attr in publishesAttrs)
                {
                    descr.Publishes.Add(attr.PublishesType.Name);
                }
            }

            return descrList;
        }

        public void EnableDisableReceptor(string typeName, bool state)
        {
            // Runner may not be up when we get this.
            appDomainRunner?.EnableDisableReceptor(typeName, state);
        }

        public object InstantiateSemanticType(string typeName)
        {
            var st = appDomainRunner.InstantiateSemanticType(typeName);

            return st;
        }

        public PropertyContainer DescribeSemanticType(string typeName)
        {
            Type semanticType = ResolveType(typeName);
            if (semanticType == null)
            {
                return null;
            }

            PropertyInfo[] properties = semanticType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var container = new PropertyContainer();
            BuildTypes(container, properties);

            return container;
        }

        public void Publish(string _, object st)
        {
            appDomainRunner.Publish((ISemanticType)st);
        }

        public void Publish(string typeName, string json)
        {
            Type semanticType = ResolveType(typeName);
            if (semanticType == null)
            {
                return;
            }

            ISemanticType semanticInstance = JsonConvert.DeserializeObject(json, semanticType) as ISemanticType;
            semanticInstance.IfNotNull(st => appDomainRunner.Publish(st));
        }

        private AppDomain CreateAppDomain(string dllName)
        {
            // AppDomain.CreateDomain is no longer supported on .NET 8.
            // Isolation/unloadability is implemented via collectible AssemblyLoadContext.
            return AppDomain.CurrentDomain;
        }

        private IHopeRunner InstantiateRunner(string assemblyPath, AppDomain domain)
        {
            HopeAssemblyLoadContext candidateLoadContext = null;
            try
            {
                candidateLoadContext = new HopeAssemblyLoadContext(assemblyPath);
                Assembly assembly = candidateLoadContext.LoadMainAssembly(assemblyPath);
                Type runnerType = assembly.GetType("HopeRunner.Runner") ??
                    assembly.GetTypes().FirstOrDefault(t =>
                        t.IsClass &&
                        !t.IsAbstract &&
                        t.GetInterfaces().Any(i => i.FullName == typeof(IHopeRunner).FullName));
                if (runnerType == null)
                {
                    UnloadCollectibleContext(candidateLoadContext);
                    return null;
                }

                IHopeRunner runner = Activator.CreateInstance(runnerType) as IHopeRunner;

                if (runner != null)
                {
                    runner.Processing += (sender, args) => Processing.Fire(this, args);
                    loadContext = candidateLoadContext;
                    loadedAssembly = assembly;
                    candidateLoadContext = null;
                }
                else
                {
                    UnloadCollectibleContext(candidateLoadContext);
                }

                return runner;
            }
            catch
            {
                if (candidateLoadContext != null)
                {
                    UnloadCollectibleContext(candidateLoadContext);
                }

                throw;
            }
        }

        protected Type ResolveType(string typeName)
        {
            if (loadedAssembly == null || string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            Type type = loadedAssembly.GetType(typeName);

            if (type != null)
            {
                return type;
            }

            return loadedAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
        }

        protected void BuildTypes(PropertyContainer container, PropertyInfo[] properties)
        {
            foreach (var property in properties)
            {
                var propertyData = new PropertyData() { Name = property.Name, TypeName = property.PropertyType.FullName };
                var category = property.GetCustomAttribute<CategoryAttribute>();
                var description = property.GetCustomAttribute<DescriptionAttribute>();
                propertyData.Category = category?.Category;
                propertyData.Description = description?.Description;
                container.Types.Add(propertyData);

                if ((!property.PropertyType.IsValueType) && (propertyData.TypeName != "System.String"))
                {
                    var childProperties = property.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    propertyData.ChildType = new PropertyContainer();
                    BuildTypes(propertyData.ChildType, childProperties);
                }
            }
        }

        /// <summary>
        /// Unexpected app domain unload.  Unfortunately, the stack trace doesn't indicate where this is coming from!
        /// </summary>
        private void AppDomainUnloading(object sender, EventArgs e)
        {
            appDomain = null;
            appDomainRunner = null;
            loadedAssembly = null;
            loadContext = null;
        }

        protected void ForceCollectForUnload()
        {
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        protected void UnloadCollectibleContext(AssemblyLoadContext context)
        {
            if (context == null)
            {
                return;
            }

            var unloadTracker = new WeakReference(context);
            context.Unload();
            context = null;
            WaitForCollectibleContextUnload(unloadTracker);
        }

        protected void WaitForCollectibleContextUnload(WeakReference unloadTracker)
        {
            for (int i = 0; i < 10 && unloadTracker.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(50);
            }
        }

        protected class HopeAssemblyLoadContext : AssemblyLoadContext
        {
            protected readonly AssemblyDependencyResolver resolver;

            public HopeAssemblyLoadContext(string mainAssemblyPath)
                : base("HopeRunnerLoadContext_" + Guid.NewGuid().ToString("N"), true)
            {
                resolver = new AssemblyDependencyResolver(mainAssemblyPath);
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                // Keep shared contracts in default context so interface/event types stay compatible.
                string hopeInterfaceName = typeof(IHopeRunner).Assembly.GetName().Name;
                string semanticsName = typeof(ISemanticType).Assembly.GetName().Name;
                string commonName = typeof(ReceptorDescription).Assembly.GetName().Name;

                if (assemblyName.Name == hopeInterfaceName || assemblyName.Name == semanticsName || assemblyName.Name == commonName)
                {
                    Assembly sharedAssembly = AssemblyLoadContext.Default.Assemblies
                        .FirstOrDefault(assembly => assembly.GetName().Name == assemblyName.Name);

                    if (sharedAssembly != null)
                    {
                        return sharedAssembly;
                    }
                }

                string assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);

                if (!string.IsNullOrEmpty(assemblyPath))
                {
                    return LoadAssemblyFromPath(assemblyPath);
                }

                return null;
            }

            public Assembly LoadMainAssembly(string assemblyPath)
            {
                return LoadAssemblyFromPath(assemblyPath);
            }

            protected Assembly LoadAssemblyFromPath(string assemblyPath)
            {
                using (var peStream = new MemoryStream(File.ReadAllBytes(assemblyPath), false))
                {
                    string pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");

                    if (File.Exists(pdbPath))
                    {
                        using (var pdbStream = new MemoryStream(File.ReadAllBytes(pdbPath), false))
                        {
                            return LoadFromStream(peStream, pdbStream);
                        }
                    }

                    return LoadFromStream(peStream);
                }
            }
        }
    }
}
