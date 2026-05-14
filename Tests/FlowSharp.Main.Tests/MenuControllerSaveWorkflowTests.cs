using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
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

        [TestMethod]
        public void MenuStrip_ExposesPrintCommandWithShortcut()
        {
            BaseController activeController = CreateController();
            var canvasService = new TestCanvasService(activeController);
            var editService = new TestEditService();
            ServiceManager serviceManager = CreateServiceManager(canvasService, editService);
            var controller = new MenuController(serviceManager);

            ToolStripMenuItem printItem = FindMenuItem(controller.MenuStrip.Items, "mnuPrint");

            Assert.IsNotNull(printItem);
            Assert.AreEqual("&Print...", printItem.Text);
            Assert.AreEqual(Keys.Control | Keys.P, printItem.ShortcutKeys);
        }

        [TestMethod]
        public void PrintCommand_CreatesPrintDocumentAndShowsDialog()
        {
            BaseController activeController = CreateController();
            AddBox(activeController);
            var canvasService = new TestCanvasService(activeController);
            var editService = new TestEditService();
            ServiceManager serviceManager = CreateServiceManager(canvasService, editService);
            var controller = new TestableMenuController(serviceManager);

            controller.ClickMenuItem("mnuPrint");

            Assert.IsTrue(controller.PrintDialogShown);
            Assert.IsNotNull(controller.PrintDialogDocument);
            Assert.AreEqual("document", controller.PrintDialogDocument.DocumentName);
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

        private static void AddBox(BaseController controller)
        {
            var box = new Box(controller.Canvas)
            {
                DisplayRectangle = new Rectangle(10, 10, 20, 20)
            };
            box.UpdatePath();
            controller.AddElement(box);
        }

        private static ToolStripMenuItem FindMenuItem(ToolStripItemCollection items, string name)
        {
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripMenuItem menuItem)
                {
                    if (menuItem.Name == name)
                    {
                        return menuItem;
                    }

                    ToolStripMenuItem child = FindMenuItem(menuItem.DropDownItems, name);
                    if (child != null)
                    {
                        return child;
                    }
                }
            }

            return null;
        }

        private sealed class TestableMenuController : MenuController
        {
            public TestableMenuController(IServiceManager serviceManager) : base(serviceManager)
            {
                InitializeMenuHandlers();
            }

            public void InvokeSaveDiagram(string filename)
            {
                SaveDiagram(filename);
            }

            public bool PrintDialogShown { get; private set; }
            public PrintDocument PrintDialogDocument { get; private set; }

            public void ClickMenuItem(string name)
            {
                ToolStripMenuItem item = FindMenuItem(MenuStrip.Items, name);
                Assert.IsNotNull(item);
                item.PerformClick();
            }

            protected override DialogResult ShowPrintDialog(PrintDocument document)
            {
                PrintDialogShown = true;
                PrintDialogDocument = document;

                return DialogResult.Cancel;
            }
        }

        private sealed class TestCanvasService : ServiceBase, IFlowSharpCanvasService
        {
            event EventHandler<EventArgs> IFlowSharpCanvasService.AddCanvas
            {
                add { }
                remove { }
            }

            event EventHandler<FileEventArgs> IFlowSharpCanvasService.LoadLayout
            {
                add { }
                remove { }
            }

            event EventHandler<FileEventArgs> IFlowSharpCanvasService.SaveLayout
            {
                add { }
                remove { }
            }

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

            public void RebaseFilenamesOnNextSave()
            {
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
