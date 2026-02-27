using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Clifton.DockingFormService;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class DockingFormServicePathTests
    {
        [TestMethod]
        public void ResolveLayoutPath_WithRelativeLayoutInAppBase_UsesAppBasePath()
        {
            string layoutFileName = "default-layout-" + Guid.NewGuid().ToString("N") + ".xml";
            string layoutPath = Path.Combine(AppContext.BaseDirectory, layoutFileName);
            string originalCurrentDirectory = Directory.GetCurrentDirectory();
            string isolatedCurrentDirectory = Path.Combine(Path.GetTempPath(), "flowsharp-layout-cwd-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(isolatedCurrentDirectory);
                Directory.SetCurrentDirectory(isolatedCurrentDirectory);
                File.WriteAllText(layoutPath, "<DockPanelFormat />");

                string resolved = new TestDockingFormService().Resolve(layoutFileName, requireExistingFile: true);

                Assert.AreEqual(layoutPath, resolved);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);

                if (File.Exists(layoutPath))
                {
                    File.Delete(layoutPath);
                }

                if (Directory.Exists(isolatedCurrentDirectory))
                {
                    Directory.Delete(isolatedCurrentDirectory, true);
                }
            }
        }

        private class TestDockingFormService : DockingFormService
        {
            public string Resolve(string filename, bool requireExistingFile)
            {
                return ResolveLayoutPath(filename, requireExistingFile);
            }
        }
    }
}
