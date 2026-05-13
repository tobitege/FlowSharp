using System;
using System.Collections.Generic;
using System.Windows.Forms;

using Clifton.Core.ServiceManagement;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpLib;
using FlowSharpServiceInterfaces;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class EditServiceDirtyStateTests
    {
        [TestMethod]
        public void CheckForChanges_ForSpecificController_IgnoresChangesOnOtherControllers()
        {
            BaseController first = CreateController();
            BaseController second = CreateController();
            var canvasService = new TestCanvasService(first, second);
            var editService = new TestableEditService(DialogResult.Yes);
            ServiceManager serviceManager = CreateServiceManager(canvasService, editService);

            editService.MarkSaved(first);
            editService.MarkSaved(second);
            MarkDirty(second);

            ClosingState state = editService.CheckForChanges(first);

            Assert.AreEqual(ClosingState.NoChanges, state);
            Assert.AreEqual(0, editService.PromptCount);
        }

        [TestMethod]
        public void CheckForChanges_ForSpecificDirtyController_ReturnsPromptDecision()
        {
            BaseController controller = CreateController();
            var canvasService = new TestCanvasService(controller);
            var editService = new TestableEditService(DialogResult.Cancel);
            ServiceManager serviceManager = CreateServiceManager(canvasService, editService);

            editService.MarkSaved(controller);
            MarkDirty(controller);

            ClosingState state = editService.CheckForChanges(controller);

            Assert.AreEqual(ClosingState.CancelClose, state);
            Assert.AreEqual(1, editService.PromptCount);
            Assert.AreSame(controller, editService.LastPromptController);
        }

        private static ServiceManager CreateServiceManager(TestCanvasService canvasService, TestableEditService editService)
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

        private static void MarkDirty(BaseController controller)
        {
            controller.UndoStack.UndoRedo("Dirty", () => { }, () => { });
        }

        private sealed class TestableEditService : FlowSharpEditService.FlowSharpEditService
        {
            private readonly DialogResult dialogResult;

            public int PromptCount { get; private set; }
            public BaseController LastPromptController { get; private set; }

            public TestableEditService(DialogResult dialogResult)
            {
                this.dialogResult = dialogResult;
            }

            public void MarkSaved(BaseController controller)
            {
                SetSavePoint(controller);
            }

            protected override DialogResult ShowSaveChangesPrompt(BaseController controller)
            {
                PromptCount++;
                LastPromptController = controller;

                return dialogResult;
            }
        }

        private sealed class TestCanvasService : ServiceBase, IFlowSharpCanvasService
        {
            public event EventHandler<EventArgs> AddCanvas;
            public event EventHandler<FileEventArgs> LoadLayout;
            public event EventHandler<FileEventArgs> SaveLayout;

            public BaseController ActiveController { get; private set; }
            public List<BaseController> Controllers { get; }

            public TestCanvasService(params BaseController[] controllers)
            {
                Controllers = new List<BaseController>(controllers);
                ActiveController = Controllers[0];
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
                throw new NotSupportedException();
            }

            public void ClearControllers()
            {
                throw new NotSupportedException();
            }
        }
    }
}
