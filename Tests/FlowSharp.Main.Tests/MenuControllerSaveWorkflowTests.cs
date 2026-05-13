using System;
using System.IO;
using System.Windows.Forms;

using Clifton.Core.ServiceManagement;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpEditService;
using FlowSharpLib;
using FlowSharpMenuService;
using FlowSharpServiceInterfaces;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class MenuControllerSaveWorkflowTests
    {
        [TestMethod]
        public void Filename_ReturnsActiveCanvasFilename()
        {
            BaseController activeController = CreateController();
            activeController.Filename = @"C:\temp\active-diagram.fsd";
            var canvasService = new TestCanvasService(activeController);
            var editService = new TestEditService();
            ServiceManager serviceManager = CreateServiceManager(canvasService, editService);

            var controller = new MenuController(serviceManager);

            Assert.AreEqual(activeController.Filename, controller.Filename);
        }

        [TestMethod]
        public void SaveDiagram_ForNormalSave_UpdatesMruWithActiveCanvasFilename()
        {
            string originalDirectory = Directory.GetCurrentDirectory();
            string tempDir = Path.Combine(Path.GetTempPath(), "flowsharp-mru-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                Directory.SetCurrentDirectory(tempDir);

                BaseController activeController = CreateController();
                activeController.Filename = Path.Combine(tempDir, "diagram.fsd");
                var canvasService = new TestCanvasService(activeController);
                var editService = new TestEditService();
                ServiceManager serviceManager = CreateServiceManager(canvasService, editService);
                var controller = new TestableMenuController(serviceManager);

                controller.InvokeSaveDiagram(activeController.Filename);

                Assert.AreEqual(activeController.Filename, canvasService.LastSavedFilename);
                Assert.IsTrue(editService.SetSavePointCalled);
                CollectionAssert.AreEqual(
                    new[] { activeController.Filename },
                    File.ReadAllLines(Path.Combine(tempDir, "FlowSharp.mru")));
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

        private static ServiceManager CreateServiceManager(TestCanvasService canvasService, TestEditService editService)
        {
            var serviceManager = new ServiceManager();
            serviceManager.RegisterSingleton<IFlowSharpCanvasService>(canvasService);
            serviceManager.RegisterSingleton<IFlowSharpEditService>(editService);
            canvasService.Initialize(serviceManager);
            editService.Initialize(serviceManager);

            return serviceManager;
        }

        private static BaseController CreateController()
        {
            var canvas = new Canvas();
            canvas.CreateBitmap(64, 64);
            return new CanvasController(canvas);
        }

        private sealed class TestableMenuController : MenuController
        {
            public TestableMenuController(IServiceManager serviceManager) : base(serviceManager)
            {
            }

            public void InvokeSaveDiagram(string filename)
            {
                SaveDiagram(filename);
            }
        }

        private sealed class TestCanvasService : ServiceBase, IFlowSharpCanvasService
        {
            public event EventHandler<EventArgs> AddCanvas;
            public event EventHandler<FileEventArgs> LoadLayout;
            public event EventHandler<FileEventArgs> SaveLayout;

            public BaseController ActiveController { get; }
            public System.Collections.Generic.List<BaseController> Controllers => new System.Collections.Generic.List<BaseController> { ActiveController };

            public string LastSavedFilename { get; private set; }
            public bool LastSelectionOnly { get; private set; }

            public TestCanvasService(BaseController activeController)
            {
                ActiveController = activeController;
            }

            public void CreateCanvas(Control parent)
            {
                throw new NotSupportedException();
            }

            public void DeleteCanvas(Control parent)
            {
                throw new NotSupportedException();
            }

            public void SetActiveController(Control parent)
            {
                throw new NotSupportedException();
            }

            public void RequestNewCanvas()
            {
                throw new NotSupportedException();
            }

            public void LoadDiagrams(string filename)
            {
                throw new NotSupportedException();
            }

            public void SaveDiagramsAndLayout(string filename, bool selectionOnly = false)
            {
                LastSavedFilename = filename;
                LastSelectionOnly = selectionOnly;
                File.WriteAllText(filename, "saved");
            }

            public void ClearControllers()
            {
                throw new NotSupportedException();
            }
        }

        private sealed class TestEditService : ServiceBase, IFlowSharpEditService
        {
            public bool SetSavePointCalled { get; private set; }

            public void NewCanvas(BaseController controller)
            {
                throw new NotSupportedException();
            }

            public void Copy()
            {
                throw new NotSupportedException();
            }

            public void Paste()
            {
                throw new NotSupportedException();
            }

            public void Delete()
            {
                throw new NotSupportedException();
            }

            public void Undo()
            {
                throw new NotSupportedException();
            }

            public void Redo()
            {
                throw new NotSupportedException();
            }

            public void EditText()
            {
                throw new NotSupportedException();
            }

            public ClosingState CheckForChanges()
            {
                throw new NotSupportedException();
            }

            public ClosingState CheckForChanges(BaseController controller)
            {
                throw new NotSupportedException();
            }

            public void ResetSavePoint()
            {
                throw new NotSupportedException();
            }

            public void ClearSavePoints()
            {
                throw new NotSupportedException();
            }

            public void SetSavePoint()
            {
                SetSavePointCalled = true;
            }

            public bool ProcessCmdKey(Keys keyData)
            {
                throw new NotSupportedException();
            }

            public void FocusOnShape(GraphicElement el)
            {
                throw new NotSupportedException();
            }
        }
    }
}
