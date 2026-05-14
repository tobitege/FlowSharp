using System.Drawing;
using System.Linq;
using System.Windows.Forms;

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

        [TestMethod]
        public void UpdateScrollbars_ForOversizedContent_ShowsScrollbarsAndClampsViewport()
        {
            using var form = new Form();
            using var parent = new Panel
            {
                Size = new Size(200, 150)
            };
            var canvas = new Canvas();
            var controller = new CanvasController(canvas);
            form.Controls.Add(parent);
            canvas.Initialize(parent);
            canvas.Size = parent.Size;

            canvas.UpdateScrollbars(new Rectangle(0, 0, 900, 700), 100);
            canvas.SetViewportOrigin(1000, 1000);
            HScrollBar horizontal = canvas.Controls.OfType<HScrollBar>().Single();
            VScrollBar vertical = canvas.Controls.OfType<VScrollBar>().Single();

            Assert.IsTrue(horizontal.Maximum > horizontal.LargeChange);
            Assert.IsTrue(vertical.Maximum > vertical.LargeChange);
            Assert.IsTrue(canvas.ViewportOrigin.X < 1000);
            Assert.IsTrue(canvas.ViewportOrigin.Y < 1000);
        }

        private static BaseController CreateController()
        {
            var canvas = new Canvas();
            canvas.CreateBitmap(128, 128);
            return new CanvasController(canvas);
        }
    }
}
