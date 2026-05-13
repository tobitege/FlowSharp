using System.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;

using Clifton.Core.ServiceManagement;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpLib;
using FlowSharpMouseControllerService;
using FlowSharpServiceInterfaces;

using WinForms = System.Windows.Forms;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class SelectionHitTestingTests
    {
        [TestMethod]
        public void GetNextRootShapeAt_CyclesOverlappingRootShapes()
        {
            BaseController controller = CreateController();
            Point clickPoint = new Point(60, 60);
            Box top = AddBox(controller, new Rectangle(40, 40, 80, 60), "top");
            Box next = AddBox(controller, new Rectangle(45, 45, 80, 60), "next");

            GraphicElement first = controller.GetNextRootShapeAt(clickPoint);
            GraphicElement second = controller.GetNextRootShapeAt(clickPoint, first);
            GraphicElement wrapped = controller.GetNextRootShapeAt(clickPoint, second);

            Assert.AreSame(top, first);
            Assert.AreSame(next, second);
            Assert.AreSame(top, wrapped);
        }

        [TestMethod]
        public void MouseClick_SelectsTopShapeOnceThenCyclesOnNextClick()
        {
            BaseController controller = CreateController();
            Point clickPoint = new Point(60, 60);
            Box top = AddBox(controller, new Rectangle(40, 40, 80, 60), "top");
            Box next = AddBox(controller, new Rectangle(45, 45, 80, 60), "next");
            TestableMouseController mouseController = CreateMouseController(controller);

            mouseController.Send(MouseController.MouseEvent.MouseDown, clickPoint);

            Assert.AreSame(top, controller.SelectedElements.Single());

            mouseController.Send(MouseController.MouseEvent.MouseUp, clickPoint);

            Assert.AreSame(top, controller.SelectedElements.Single());

            mouseController.Send(MouseController.MouseEvent.MouseDown, clickPoint);
            mouseController.Send(MouseController.MouseEvent.MouseUp, clickPoint);

            Assert.AreSame(next, controller.SelectedElements.Single());
        }

        [TestMethod]
        public void MouseUp_OnEmptyCanvas_DoesNotTryToSelectRootShape()
        {
            BaseController controller = CreateController();
            TestableMouseController mouseController = CreateMouseController(controller);

            mouseController.Send(MouseController.MouseEvent.MouseUp, new Point(200, 160));

            Assert.AreEqual(0, controller.SelectedElements.Count);
        }

        [TestMethod]
        public void GetSelectableShapesAt_LimitsNestedHitTestingToOneLevelDeep()
        {
            BaseController controller = CreateController();

            GroupBox rootGroup = new GroupBox(controller.Canvas)
            {
                DisplayRectangle = new Rectangle(20, 20, 180, 140)
            };
            rootGroup.UpdateZoomRectangle();

            GroupBox nestedGroup = new GroupBox(controller.Canvas)
            {
                DisplayRectangle = new Rectangle(40, 40, 120, 90),
                Parent = rootGroup
            };
            nestedGroup.UpdateZoomRectangle();
            rootGroup.GroupChildren.Add(nestedGroup);

            Box deepChild = new Box(controller.Canvas)
            {
                DisplayRectangle = new Rectangle(60, 60, 40, 30),
                Parent = nestedGroup
            };
            deepChild.UpdateZoomRectangle();
            nestedGroup.GroupChildren.Add(deepChild);

            controller.AddElement(rootGroup);
            controller.AddElement(nestedGroup);
            controller.AddElement(deepChild);

            var selectable = controller.GetSelectableShapesAt(new Point(70, 70)).ToList();

            CollectionAssert.AreEqual(new GraphicElement[] { rootGroup, nestedGroup }, selectable);
            Assert.AreSame(nestedGroup, controller.GetChildShapeAt(new Point(70, 70)));
            Assert.IsFalse(selectable.Contains(deepChild));
        }

        [TestMethod]
        public void FindAllIntersections_WithDenseOverlap_DoesNotUseRecursiveCallStack()
        {
            BaseController controller = CreateController();
            Box topmost = null;

            for (int i = 0; i < 2048; i++)
            {
                topmost = AddBox(controller, new Rectangle(40, 40, 80, 60), i.ToString());
            }

            var intersections = controller.FindAllIntersections(topmost).ToList();

            Assert.AreEqual(2048, intersections.Count);
        }

        private static BaseController CreateController()
        {
            var canvas = new Canvas();
            canvas.CreateBitmap(240, 180);
            return new CanvasController(canvas);
        }

        private static Box AddBox(BaseController controller, Rectangle rectangle, string text)
        {
            var box = new Box(controller.Canvas)
            {
                DisplayRectangle = rectangle,
                Text = text
            };
            box.UpdateZoomRectangle();
            controller.AddElement(box);

            return box;
        }

        private static TestableMouseController CreateMouseController(BaseController controller)
        {
            var serviceManager = new ServiceManager();
            var canvasService = new TestCanvasService(controller);
            serviceManager.RegisterSingleton<IFlowSharpCanvasService>(canvasService);

            var mouseController = new TestableMouseController(serviceManager);
            mouseController.InitializeBehavior();

            return mouseController;
        }

        private sealed class TestableMouseController : MouseController
        {
            public TestableMouseController(IServiceManager serviceManager) : base(serviceManager)
            {
            }

            public void Send(MouseEvent mouseEvent, Point point)
            {
                var args = new WinForms.MouseEventArgs(WinForms.MouseButtons.Left, 1, point.X, point.Y, 0);
                HandleEvent(new MouseAction(mouseEvent, args));
            }
        }

        private sealed class TestCanvasService : ServiceBase, IFlowSharpCanvasService
        {
            public event EventHandler<EventArgs> AddCanvas;
            public event EventHandler<FileEventArgs> LoadLayout;
            public event EventHandler<FileEventArgs> SaveLayout;

            public BaseController ActiveController { get; }
            public List<BaseController> Controllers { get; }

            public TestCanvasService(BaseController controller)
            {
                ActiveController = controller;
                Controllers = new List<BaseController>() { controller };
            }

            public void CreateCanvas(WinForms.Control parent)
            {
                throw new NotSupportedException();
            }

            public void DeleteCanvas(WinForms.Control parent)
            {
                throw new NotSupportedException();
            }

            public void SetActiveController(WinForms.Control parent)
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
