using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpToolboxService;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class PluginManagerPathTests
    {
        [TestMethod]
        public void ResolvePluginFileListPath_WithRelativeMissingInCwd_UsesAppBase()
        {
            string pluginListPath = Path.Combine(AppContext.BaseDirectory, "plugins.txt");
            string originalCurrentDirectory = Directory.GetCurrentDirectory();
            string isolatedCurrentDirectory = Path.Combine(Path.GetTempPath(), "flowsharp-plugin-cwd-" + Guid.NewGuid().ToString("N"));
            bool createdPluginList = false;

            try
            {
                Directory.CreateDirectory(isolatedCurrentDirectory);
                Directory.SetCurrentDirectory(isolatedCurrentDirectory);

                if (!File.Exists(pluginListPath))
                {
                    File.WriteAllText(pluginListPath, "");
                    createdPluginList = true;
                }

                string resolved = new TestPluginManager().ResolvePluginListPath();

                Assert.AreEqual(pluginListPath, resolved);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);

                if (createdPluginList && File.Exists(pluginListPath))
                {
                    File.Delete(pluginListPath);
                }

                if (Directory.Exists(isolatedCurrentDirectory))
                {
                    Directory.Delete(isolatedCurrentDirectory, true);
                }
            }
        }

        [TestMethod]
        public void ResolvePluginAssemblyPath_WithRelativePath_UsesPluginListDirectory()
        {
            string pluginListPath = Path.Combine(AppContext.BaseDirectory, "plugins.txt");
            string originalCurrentDirectory = Directory.GetCurrentDirectory();
            string isolatedCurrentDirectory = Path.Combine(Path.GetTempPath(), "flowsharp-plugin-path-cwd-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(isolatedCurrentDirectory);
                Directory.SetCurrentDirectory(isolatedCurrentDirectory);

                string resolved = new TestPluginManager().ResolvePluginPath(pluginListPath, "PluginExample.dll");
                string expected = Path.Combine(AppContext.BaseDirectory, "PluginExample.dll");

                Assert.AreEqual(expected, resolved);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);

                if (Directory.Exists(isolatedCurrentDirectory))
                {
                    Directory.Delete(isolatedCurrentDirectory, true);
                }
            }
        }

        [TestMethod]
        public void ResolvePluginAssemblyPath_WithCwdPluginListAndMissingRelativeAssembly_FallsBackToAppBase()
        {
            string originalCurrentDirectory = Directory.GetCurrentDirectory();
            string isolatedCurrentDirectory = Path.Combine(Path.GetTempPath(), "flowsharp-plugin-fallback-cwd-" + Guid.NewGuid().ToString("N"));
            string pluginAssemblyName = "PluginExample.dll";
            string pluginListFile = "plugins.txt";
            string appBasePluginPath = Path.Combine(AppContext.BaseDirectory, pluginAssemblyName);
            bool createdAppBasePlugin = false;

            try
            {
                Directory.CreateDirectory(isolatedCurrentDirectory);
                Directory.SetCurrentDirectory(isolatedCurrentDirectory);
                File.WriteAllText(Path.Combine(isolatedCurrentDirectory, pluginListFile), pluginAssemblyName);

                if (!File.Exists(appBasePluginPath))
                {
                    File.WriteAllText(appBasePluginPath, String.Empty);
                    createdAppBasePlugin = true;
                }

                string pluginListPath = new TestPluginManager().ResolvePluginListPath();
                string resolved = new TestPluginManager().ResolvePluginPath(pluginListPath, pluginAssemblyName);

                Assert.AreEqual(appBasePluginPath, resolved);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);

                if (createdAppBasePlugin && File.Exists(appBasePluginPath))
                {
                    File.Delete(appBasePluginPath);
                }

                if (Directory.Exists(isolatedCurrentDirectory))
                {
                    Directory.Delete(isolatedCurrentDirectory, true);
                }
            }
        }

        private class TestPluginManager : PluginManager
        {
            public string ResolvePluginListPath()
            {
                return ResolvePluginFileListPath();
            }

            public string ResolvePluginPath(string pluginListPath, string pluginAssemblyPath)
            {
                return ResolvePluginAssemblyPath(pluginListPath, pluginAssemblyPath);
            }
        }
    }
}
