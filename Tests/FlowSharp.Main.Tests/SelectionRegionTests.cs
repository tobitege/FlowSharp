using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpLib;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class SelectionRegionTests
    {
        [TestMethod]
        public void GetShapesInSelectionRegion_SelectsIntersectingRootShapes()
        {
            BaseController controller = CreateController();
            Box shape = AddBox(controller, new Rectangle(50, 50, 80, 40), "root");

            List<GraphicElement> selected = controller.GetShapesInSelectionRegion(new Rectangle(40, 40, 20, 20));

            CollectionAssert.AreEquivalent(new[] { shape }, selected);
        }

        [TestMethod]
        public void GetShapesInSelectionRegion_ExcludesGroupedChildren()
        {
            BaseController controller = CreateController();
            GroupBox group = new GroupBox(controller.Canvas)
            {
                DisplayRectangle = new Rectangle(40, 40, 140, 120)
            };
            group.UpdateZoomRectangle();
            Box child = new Box(controller.Canvas)
            {
                DisplayRectangle = new Rectangle(70, 70, 40, 30)
            };
            child.UpdateZoomRectangle();
            child.Parent = group;
            group.GroupChildren.Add(child);
            controller.AddElement(group);
            controller.AddElement(child);

            List<GraphicElement> selected = controller.GetShapesInSelectionRegion(new Rectangle(65, 65, 30, 30));

            Assert.IsFalse(selected.Contains(child));
            Assert.IsTrue(selected.All(e => e.Parent == null));
        }

        private static BaseController CreateController()
        {
            var canvas = new Canvas();
            canvas.CreateBitmap(220, 180);
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
    }
}
