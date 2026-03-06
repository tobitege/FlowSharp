using System.Drawing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpLib;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class CanvasViewTests
    {
        [TestMethod]
        public void SetCanvasOffset_TracksTranslationAndMovesRootElements()
        {
            BaseController controller = CreateController();
            var box = new Box(controller.Canvas)
            {
                DisplayRectangle = new Rectangle(10, 20, 60, 40)
            };

            controller.AddElement(box);
            controller.SetCanvasOffset(new Point(15, -5));

            Assert.AreEqual(new Point(15, -5), controller.CanvasOffset);
            Assert.AreEqual(new Point(25, 15), box.DisplayRectangle.Location);
        }

        [TestMethod]
        public void Clear_ResetsCanvasOffset()
        {
            BaseController controller = CreateController();
            controller.AddElement(new Box(controller.Canvas)
            {
                DisplayRectangle = new Rectangle(10, 20, 60, 40)
            });

            controller.SetCanvasOffset(new Point(12, 8));
            controller.Clear();

            Assert.AreEqual(Point.Empty, controller.CanvasOffset);
        }

        [TestMethod]
        public void SetZoom_UpdatesZoom()
        {
            BaseController controller = CreateController();

            controller.SetZoom(80);

            Assert.AreEqual(80, controller.Zoom);
        }

        private static BaseController CreateController()
        {
            var canvas = new Canvas();
            canvas.CreateBitmap(128, 128);
            return new CanvasController(canvas);
        }
    }
}
