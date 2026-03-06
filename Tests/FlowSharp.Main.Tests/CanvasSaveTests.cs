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

        [TestMethod]
        public void SaveDiagrams_WhenRebasingMultipleCanvases_UsesActiveControllerAsBaseFile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "flowsharp-rebase-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string saveFilename = Path.Combine(tempDir, "diagram.fsd");
                BaseController first = CreateControllerWithBox("first");
                BaseController second = CreateControllerWithBox("second");
                var service = new TestFlowSharpCanvasService();

                service.AddController(new Panel(), first);
                service.AddController(new Panel(), second);
                service.SetActiveControllerForTest(second);
                service.RebaseFilenamesOnNextSave();

                service.SaveAll(saveFilename);

                string baseFileContents = File.ReadAllText(saveFilename);
                string siblingFileContents = File.ReadAllText(Path.Combine(tempDir, "diagram-1.fsd"));

                StringAssert.Contains(baseFileContents, "second");
                StringAssert.Contains(siblingFileContents, "first");
                Assert.AreEqual(saveFilename, second.Filename);
                Assert.AreEqual(Path.Combine(tempDir, "diagram-1.fsd"), first.Filename);
            }
            finally
            {
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

        private static BaseController CreateControllerWithBox(string text)
        {
            BaseController controller = CreateController();
            controller.AddElement(new Box(controller.Canvas)
            {
                Text = text,
                DisplayRectangle = new System.Drawing.Rectangle(10, 10, 60, 40)
            });

            return controller;
        }

        private sealed class TestFlowSharpCanvasService : FlowSharpCanvasService.FlowSharpCanvasService
        {
            public void AddController(Control key, BaseController controller)
            {
                documents[key] = controller;
            }

            public void SetActiveControllerForTest(BaseController controller)
            {
                activeCanvasController = controller;
            }

            public void SaveAll(string filename)
            {
                SaveDiagrams(filename);
            }
        }
    }
}
