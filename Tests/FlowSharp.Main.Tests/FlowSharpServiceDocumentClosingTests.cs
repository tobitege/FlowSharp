using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using Clifton.Core.ServiceManagement;
using Clifton.DockingFormService;
using Clifton.WinForm.ServiceInterfaces;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpLib;
using FlowSharpService;
using FlowSharpServiceInterfaces;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class FlowSharpServiceDocumentClosingTests
    {
        [TestMethod]
        public void DocumentClosing_WhenDirtyCanvasSaveAccepted_SavesAndClosesDocument()
        {
            BaseController controller = CreateController();
            Control canvasHost = CreateCanvasDocumentHost(controller);
            var editService = new TestEditService(ClosingState.SaveChanges);
            var menuService = new TestMenuService(true);
            var canvasService = new TestCanvasService(controller);
            var service = CreateService(editService, menuService, canvasService);
            var args = CreateClosingArgs(canvasHost);

            service.InvokeOnDocumentClosing(args);

            Assert.IsFalse(args.Cancel);
            Assert.AreSame(canvasHost, canvasService.LastActiveParent);
            Assert.AreSame(canvasHost, canvasService.LastDeletedParent);
            Assert.AreEqual(1, menuService.SaveOrSaveAsCount);
            Assert.AreSame(controller, editService.LastCheckedController);
        }

        [TestMethod]
        public void DocumentClosing_WhenDirtyCanvasSaveCancelled_CancelsCloseAndKeepsDocument()
        {
            BaseController controller = CreateController();
            Control canvasHost = CreateCanvasDocumentHost(controller);
            var editService = new TestEditService(ClosingState.SaveChanges);
            var menuService = new TestMenuService(false);
            var canvasService = new TestCanvasService(controller);
            var service = CreateService(editService, menuService, canvasService);
            var args = CreateClosingArgs(canvasHost);

            service.InvokeOnDocumentClosing(args);

            Assert.IsTrue(args.Cancel);
            Assert.AreSame(canvasHost, canvasService.LastActiveParent);
            Assert.IsNull(canvasService.LastDeletedParent);
            Assert.AreEqual(1, menuService.SaveOrSaveAsCount);
            Assert.AreSame(controller, editService.LastCheckedController);
        }

        [TestMethod]
        public void DocumentClosing_WhenPromptCancelled_CancelsCloseWithoutSaving()
        {
            BaseController controller = CreateController();
            Control canvasHost = CreateCanvasDocumentHost(controller);
            var editService = new TestEditService(ClosingState.CancelClose);
            var menuService = new TestMenuService(true);
            var canvasService = new TestCanvasService(controller);
            var service = CreateService(editService, menuService, canvasService);
            var args = CreateClosingArgs(canvasHost);

            service.InvokeOnDocumentClosing(args);

            Assert.IsTrue(args.Cancel);
            Assert.IsNull(canvasService.LastActiveParent);
            Assert.IsNull(canvasService.LastDeletedParent);
            Assert.AreEqual(0, menuService.SaveOrSaveAsCount);
            Assert.AreSame(controller, editService.LastCheckedController);
        }

        private static TestableFlowSharpService CreateService(
            IFlowSharpEditService editService,
            IFlowSharpMenuService menuService,
            IFlowSharpCanvasService canvasService)
        {
            var serviceManager = new ServiceManager();
            serviceManager.RegisterSingleton<IFlowSharpCanvasService>(canvasService);
            serviceManager.RegisterSingleton<IFlowSharpEditService>(editService);
            serviceManager.RegisterSingleton<IFlowSharpMenuService>(menuService);

            var service = new TestableFlowSharpService();
            service.Initialize(serviceManager);

            return service;
        }

        private static BaseController CreateController()
        {
            var canvas = new Canvas();
            canvas.CreateBitmap(64, 64);
            return new CanvasController(canvas);
        }

        private static Control CreateCanvasDocumentHost(BaseController controller)
        {
            var canvasHost = new Panel();
            canvasHost.Controls.Add(controller.Canvas);
            return canvasHost;
        }

        private static DockDocumentClosingEventArgs CreateClosingArgs(Control canvasHost)
        {
            var dockContent = new GenericDockContent(Constants.META_CANVAS);
            dockContent.Controls.Add(canvasHost);

            return new DockDocumentClosingEventArgs
            {
                DockContent = dockContent,
                CloseReason = CloseReason.UserClosing
            };
        }

        private sealed class TestableFlowSharpService : FlowSharpService.FlowSharpService
        {
            public void InvokeOnDocumentClosing(DockDocumentClosingEventArgs args)
            {
                OnDocumentClosing(this, args);
            }
        }

        private sealed class TestEditService : ServiceBase, IFlowSharpEditService
        {
            private readonly ClosingState closingState;

            public BaseController LastCheckedController { get; private set; }

            public TestEditService(ClosingState closingState)
            {
                this.closingState = closingState;
            }

            public ClosingState CheckForChanges(BaseController controller)
            {
                LastCheckedController = controller;
                return closingState;
            }

            public void NewCanvas(BaseController controller) { throw new NotSupportedException(); }
            public void Copy() { throw new NotSupportedException(); }
            public void Paste() { throw new NotSupportedException(); }
            public void Delete() { throw new NotSupportedException(); }
            public void Undo() { throw new NotSupportedException(); }
            public void Redo() { throw new NotSupportedException(); }
            public void EditText() { throw new NotSupportedException(); }
            public ClosingState CheckForChanges() { throw new NotSupportedException(); }
            public void ResetSavePoint() { throw new NotSupportedException(); }
            public void ClearSavePoints() { throw new NotSupportedException(); }
            public void SetSavePoint() { throw new NotSupportedException(); }
            public bool ProcessCmdKey(Keys keyData) { throw new NotSupportedException(); }
            public void FocusOnShape(GraphicElement el) { throw new NotSupportedException(); }
        }

        private sealed class TestMenuService : ServiceBase, IFlowSharpMenuService
        {
            private readonly bool saveResult;

            public string Filename { get; }
            public int SaveOrSaveAsCount { get; private set; }

            public TestMenuService(bool saveResult)
            {
                this.saveResult = saveResult;
            }

            public bool SaveOrSaveAs()
            {
                SaveOrSaveAsCount++;
                return saveResult;
            }

            public void Initialize(Form mainForm) { throw new NotSupportedException(); }
            public void Initialize(BaseController controller) { throw new NotSupportedException(); }
            public void UpdateMenu() { throw new NotSupportedException(); }
            public void AddMenu(ToolStripMenuItem menuItem) { throw new NotSupportedException(); }
            public void EnableCopyPasteDel(bool state) { throw new NotSupportedException(); }
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

            public BaseController ActiveController { get; private set; }
            public List<BaseController> Controllers { get; }
            public Control LastActiveParent { get; private set; }
            public Control LastDeletedParent { get; private set; }

            public TestCanvasService(BaseController activeController)
            {
                ActiveController = activeController;
                Controllers = new List<BaseController> { activeController };
            }

            public void SetActiveController(Control parent)
            {
                LastActiveParent = parent;
            }

            public void DeleteCanvas(Control parent)
            {
                LastDeletedParent = parent;
            }

            public void CreateCanvas(Control parent) { throw new NotSupportedException(); }
            public void RequestNewCanvas() { throw new NotSupportedException(); }
            public void LoadDiagrams(string filename) { throw new NotSupportedException(); }
            public void SaveDiagramsAndLayout(string filename, bool selectionOnly = false) { throw new NotSupportedException(); }
            public void RebaseFilenamesOnNextSave() { throw new NotSupportedException(); }
            public void ClearControllers() { throw new NotSupportedException(); }
        }
    }
}
