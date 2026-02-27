using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;

using Clifton.Core.Assertions;
using Clifton.Core.ExtensionMethods;
using Clifton.Core.Semantics;
using Clifton.Core.ModuleManagement;
using Clifton.Core.ServiceManagement;

namespace FlowSharp
{
    static partial class Program
    {
        public static ServiceManager ServiceManager;
        internal static Action<string> BootstrapCore = DefaultBootstrapCore;
        internal static Action<string, string, MessageBoxButtons, MessageBoxIcon> ShowMessageBox =
            (text, caption, buttons, icon) => MessageBox.Show(text, caption, buttons, icon);

        static void Bootstrap(string moduleFilename = "modules.xml")
        {
            ServiceManager = new ServiceManager();
            ServiceManager.RegisterSingleton<IServiceModuleManager, ServiceModuleManager>();

            try
            {
                BootstrapCore(moduleFilename);
            }
            catch(ReflectionTypeLoadException lex)
            {
                StringBuilder sb = new StringBuilder();

                foreach (Exception ex in lex.LoaderExceptions)
                {
                    sb.AppendLine(ex.Message);
                }

                ShowMessageBox(sb.ToString(), "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                ShowMessageBox(ex.Message + "\r\n" + ex.StackTrace, "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void DefaultBootstrapCore(string moduleFilename)
        {
            IModuleManager moduleMgr = (IModuleManager)ServiceManager.Get<IServiceModuleManager>();
            List<AssemblyFileName> modules = GetModuleList(XmlFileName.Create(moduleFilename));
            moduleMgr.RegisterModules(modules);
            List<Exception> exceptions = ServiceManager.FinishSingletonInitialization();
            ShowAnyExceptions(exceptions);
        }

        /// <summary>
        /// Return the list of assembly names specified in the XML file so that
        /// we know what assemblies are considered modules as part of the application.
        /// </summary>
        static private List<AssemblyFileName> GetModuleList(XmlFileName filename)
        {
            string moduleDefinitionFile = ResolveModuleDefinitionPath(filename.Value);
            Assert.That(File.Exists(moduleDefinitionFile), "Module definition file " + moduleDefinitionFile + " does not exist.");
            XDocument xdoc = XDocument.Load(moduleDefinitionFile);

            return GetModuleList(xdoc);
        }

        /// <summary>
        /// Resolve module definition path robustly for shell launches where the working directory
        /// differs from the application base directory.
        /// </summary>
        static private string ResolveModuleDefinitionPath(string moduleFilename)
        {
            if (Path.IsPathRooted(moduleFilename))
            {
                return moduleFilename;
            }

            if (File.Exists(moduleFilename))
            {
                return moduleFilename;
            }

            string appBasePath = AppContext.BaseDirectory;
            string modulePathInAppBase = Path.Combine(appBasePath, moduleFilename);

            if (File.Exists(modulePathInAppBase))
            {
                return modulePathInAppBase;
            }

            return moduleFilename;
        }

        /// <summary>
        /// Returns the list of modules specified in the XML document so we know what
        /// modules to instantiate.
        /// </summary>
        static private List<AssemblyFileName> GetModuleList(XDocument xdoc)
        {
            List<AssemblyFileName> assemblies = new List<AssemblyFileName>();
            (from module in xdoc.Element("Modules").Elements("Module")
             select module.Attribute("AssemblyName").Value).ForEach(s => assemblies.Add(AssemblyFileName.Create(s)));

            return assemblies;
        }
    }
}
