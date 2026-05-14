using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using Clifton.Core.ServiceManagement;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpLib;
using FlowSharpServiceInterfaces;
using FlowSharpToolboxService;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class ToolboxDragTests
    {
        [TestMethod]
        public void CreateShapeAtCanvasPoint_CentersToolboxShapeOnSurfaceThroughViewportAndZoom()
        {
            ServiceManager serviceManager = new ServiceManager();
            Canvas surface = CreateCanvas(600, 400);
            CanvasController surfaceController = new CanvasController(surface);
            TestCanvasService canvasService = new TestCanvasService(surfaceController);
            TestDebugWindowService debugWindowService = new TestDebugWindowService();
            serviceManager.RegisterSingleton<IFlowSharpCanvasService>(canvasService);
            serviceManager.RegisterSingleton<IFlowSharpDebugWindowService>(debugWindowService);

            surface.UpdateScrollbars(new Rectangle(0, 0, 1200, 900), 100);
            surface.SetViewportOrigin(40, 30);
            surfaceController.SetZoom(200);

            Canvas toolboxSurface = CreateCanvas(120, 80);
            TestableToolboxController toolboxController = new TestableToolboxController(serviceManager, toolboxSurface);
            Box toolboxShape = new Box(toolboxSurface)
            {
                DisplayRectangle = new Rectangle(10, 10, 25, 25)
            };
            toolboxShape.UpdatePath();
            toolboxController.AddElement(toolboxShape);
            toolboxController.SelectElement(toolboxShape);

            GraphicElement created = toolboxController.CreateShapeAt(new Point(160, 130));

            Assert.AreEqual(new Rectangle(70, 50, 60, 60), created.DisplayRectangle);
            Assert.AreSame(created, surfaceController.SelectedElements[0]);
            Assert.AreEqual(1, debugWindowService.UpdateDebugWindowCount);
            Assert.IsTrue(surfaceController.UndoStack.CanUndo);
        }

        private static Canvas CreateCanvas(int width, int height)
        {
            Canvas canvas = new Canvas
            {
                Width = width,
                Height = height
            };
            canvas.CreateBitmap(width, height);

            return canvas;
        }

        private sealed class TestableToolboxController : ToolboxController
        {
            public TestableToolboxController(IServiceManager serviceManager, Canvas canvas) : base(serviceManager, canvas)
            {
            }

            public GraphicElement CreateShapeAt(Point canvasClientPoint)
            {
                return CreateShape(canvasClientPoint);
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

            public TestCanvasService(BaseController activeController)
            {
                ActiveController = activeController;
            }

            public BaseController ActiveController { get; }

            public List<BaseController> Controllers => new List<BaseController> { ActiveController };

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

            public void RebaseFilenamesOnNextSave()
            {
                throw new NotSupportedException();
            }

            public void ClearControllers()
            {
                throw new NotSupportedException();
            }
        }

        private sealed class TestDebugWindowService : ServiceBase, IFlowSharpDebugWindowService
        {
            public int UpdateDebugWindowCount { get; private set; }

            public void Initialize(BaseController canvasController)
            {
                throw new NotSupportedException();
            }

            public void ShowDebugWindow()
            {
                throw new NotSupportedException();
            }

            public void EditPlugins()
            {
                throw new NotSupportedException();
            }

            public void UpdateDebugWindow()
            {
                UpdateDebugWindowCount++;
            }

            public void UpdateShapeTree()
            {
                throw new NotSupportedException();
            }

            public void UpdateStackTrace()
            {
                throw new NotSupportedException();
            }

            public void FindShape(GraphicElement shape)
            {
                throw new NotSupportedException();
            }
        }
    }
}
