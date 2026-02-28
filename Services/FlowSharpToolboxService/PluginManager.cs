using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

using Clifton.Core.ExtensionMethods;

using FlowSharpLib;
using FlowSharpServiceInterfaces;

namespace FlowSharpToolboxService
{
    public class PluginManager
    {
        protected List<Type> pluginShapes = new List<Type>();
        protected List<Assembly> pluginAssemblies = new List<Assembly>();
        protected List<string> pluginFiles = new List<string>();

        public PluginManager()
        {
            Persist.AssemblyResolver = AssemblyResolver;
            Persist.TypeResolver = TypeResolver;
        }

        public void InitializePlugins()
        {
            string pluginFileListPath = ResolvePluginFileListPath();

            if (File.Exists(pluginFileListPath))
            {
                string[] plugins = File.ReadAllLines(pluginFileListPath);

                foreach (string plugin in plugins.Where(p => !String.IsNullOrWhiteSpace(p) && !p.BeginsWith("#")))
                {
                    RegisterPlugin(ResolvePluginAssemblyPath(pluginFileListPath, plugin));
                }
            }
        }

        public void UpdatePlugins()
        {
            string pluginFileListPath = ResolvePluginFileListPath();

            if (File.Exists(pluginFileListPath))
            {
                string[] plugins = File.ReadAllLines(pluginFileListPath);

                foreach (string plugin in plugins.Where(p => !String.IsNullOrWhiteSpace(p) && !p.BeginsWith("#")))
                {
                    string resolvedPluginPath = ResolvePluginAssemblyPath(pluginFileListPath, plugin);

                    if (!pluginFiles.Contains(resolvedPluginPath))
                    {
                        RegisterPlugin(resolvedPluginPath);
                    }
                }
            }
        }

        public List<Type> GetShapeTypes()
        {
            return pluginShapes;
        }

        protected void RegisterPlugin(string plugin)
        {
            try
            {
                Assembly assy = Assembly.LoadFrom(plugin);
                pluginAssemblies.Add(assy);

                assy.GetTypes().ForEach(t =>
                {
                    if (t.IsSubclassOf(typeof(GraphicElement)))
                    {
                        pluginShapes.Add(t);
                    }
                });

                pluginFiles.Add(plugin);
            }
            catch (Exception ex)
            {
                MessageBox.Show(plugin + "\r\n" + ex.Message, "Plugin Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected virtual string ResolvePluginFileListPath()
        {
            if (File.Exists(Constants.PLUGIN_FILE_LIST))
            {
                return Path.GetFullPath(Constants.PLUGIN_FILE_LIST);
            }

            string appBasePath = AppContext.BaseDirectory;
            return Path.Combine(appBasePath, Constants.PLUGIN_FILE_LIST);
        }

        protected virtual string ResolvePluginAssemblyPath(string pluginFileListPath, string pluginAssemblyPath)
        {
            if (Path.IsPathRooted(pluginAssemblyPath))
            {
                return pluginAssemblyPath;
            }

            if (File.Exists(pluginAssemblyPath))
            {
                return Path.GetFullPath(pluginAssemblyPath);
            }

            string pluginListDirectory = Path.GetDirectoryName(pluginFileListPath);

            if (String.IsNullOrWhiteSpace(pluginListDirectory))
            {
                pluginListDirectory = AppContext.BaseDirectory;
            }

            string pluginPathFromListDirectory = Path.Combine(pluginListDirectory, pluginAssemblyPath);

            if (File.Exists(pluginPathFromListDirectory))
            {
                return pluginPathFromListDirectory;
            }

            string appBasePluginPath = Path.Combine(AppContext.BaseDirectory, pluginAssemblyPath);
            return appBasePluginPath;
        }

        protected Assembly AssemblyResolver(AssemblyName assyName)
        {
            Assembly ret = null;

            foreach (Assembly assy in pluginAssemblies)
            {
                if (assy.FullName == assyName.FullName)
                {
                    ret = assy;
                    break;
                }
            }

            if (ret == null)
            {
                ret = Assembly.Load(assyName);
            }

            return ret;
        }

        protected Type TypeResolver(Assembly assy, string typeName, bool ignoreCase)
        {
            return assy.GetType(typeName);
        }
    }
}
