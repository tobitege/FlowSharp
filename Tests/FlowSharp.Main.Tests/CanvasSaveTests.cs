using System;
using System.IO;
using System.Windows.Forms;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpLib;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class CanvasSaveTests
    {
        [TestMethod]
        public void SaveDiagrams_WithUnnamedSecondCanvas_AssignsSiblingFilenameAndDoesNotThrow()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "flowsharp-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string firstFilename = Path.Combine(tempDir, "first.fsd");
                string saveFilename = Path.Combine(tempDir, "diagram.fsd");

                var service = new TestFlowSharpCanvasService();
                BaseController first = CreateController();
                first.Filename = firstFilename;
                BaseController second = CreateController();

                service.AddController(new Panel(), first);
                service.AddController(new Panel(), second);

                service.SaveAll(saveFilename);

                Assert.AreEqual(Path.Combine(tempDir, "diagram-1.fsd"), second.Filename);
                Assert.IsTrue(File.Exists(firstFilename));
                Assert.IsTrue(File.Exists(second.Filename));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void SaveDiagrams_WithRelativeBaseFilename_SavesUnnamedCanvasNextToBaseFile()
        {
            string originalDirectory = Directory.GetCurrentDirectory();
            string tempDir = Path.Combine(Path.GetTempPath(), "flowsharp-relative-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                Directory.SetCurrentDirectory(tempDir);

                var service = new TestFlowSharpCanvasService();
                BaseController first = CreateController();
                BaseController second = CreateController();

                service.AddController(new Panel(), first);
                service.AddController(new Panel(), second);

                service.SaveAll("diagram.fsd");

                Assert.AreEqual("diagram.fsd", first.Filename);
                Assert.AreEqual("diagram-1.fsd", second.Filename);
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, "diagram.fsd")));
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, "diagram-1.fsd")));
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);

                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private static BaseController CreateController()
        {
            var canvas = new Canvas();
            canvas.CreateBitmap(32, 32);
            return new CanvasController(canvas);
        }

        private sealed class TestFlowSharpCanvasService : FlowSharpCanvasService.FlowSharpCanvasService
        {
            public void AddController(Control key, BaseController controller)
            {
                documents[key] = controller;
            }

            public void SaveAll(string filename)
            {
                SaveDiagrams(filename);
            }
        }
    }
}
