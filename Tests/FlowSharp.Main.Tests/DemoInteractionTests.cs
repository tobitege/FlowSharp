using System.Drawing;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpLib;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class DemoInteractionTests
    {
        [TestMethod]
        public void HelloFlowSharpCodeDemo_AllowsAddedEllipseMoveAndResize()
        {
            BaseController controller = CreateController();
            string demoPath = FindRepoFile("Demos", "Hello FlowSharpCode.fsd");
            string data = File.ReadAllText(demoPath);
            var elements = Persist.Deserialize(controller.Canvas, data);
            controller.AddElements(elements);

            var ellipse = new Ellipse(controller.Canvas)
            {
                DisplayRectangle = new Rectangle(380, 45, 60, 60)
            };
            ellipse.UpdateZoomRectangle();
            controller.AddElement(ellipse);

            controller.SelectElement(ellipse);
            controller.MoveSelectedElements(new Point(10, 5));
            ellipse.UpdateSize(ellipse.GetBottomRightAnchor(), new Point(12, 8));

            Assert.AreEqual(new Rectangle(390, 50, 72, 68), ellipse.DisplayRectangle);
            Assert.AreEqual(2, controller.Elements.Count);
            Assert.IsTrue(controller.Elements.OfType<Box>().Any(el => el.Text == "Hello FlowSharpCode!"));
        }

        private static BaseController CreateController()
        {
            var canvas = new Canvas();
            canvas.CreateBitmap(640, 360);
            return new CanvasController(canvas);
        }

        private static string FindRepoFile(params string[] parts)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (directory != null)
            {
                string path = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());

                if (File.Exists(path))
                {
                    return path;
                }

                directory = directory.Parent;
            }

            Assert.Fail("Could not find repo file: " + Path.Combine(parts));
            return null;
        }
    }
}
