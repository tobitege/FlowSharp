using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpLib;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class BacklogFeatureTests
    {
        [TestMethod]
        public void ViewportOrigin_ConvertsBetweenClientAndWorldCoordinates()
        {
            BaseController controller = CreateController(600, 400);
            controller.Canvas.UpdateScrollbars(new Rectangle(0, 0, 1200, 900), 100);
            controller.Canvas.SetViewportOrigin(30, 20);

            Point client = controller.WorldToClient(new Point(100, 80));
            Point world = controller.ClientToWorld(client);

            Assert.AreEqual(new Point(70, 60), client);
            Assert.AreEqual(new Point(100, 80), world);
        }

        [TestMethod]
        public void SetZoom_UpdatesViewportAwareZoomRectangle()
        {
            BaseController controller = CreateController(600, 400);
            Box box = AddBox(controller, new Rectangle(100, 80, 50, 40));

            controller.Canvas.UpdateScrollbars(new Rectangle(0, 0, 1200, 900), 100);
            controller.Canvas.SetViewportOrigin(30, 20);
            controller.SetZoom(200);

            Assert.AreEqual(new Rectangle(170, 140, 100, 80), box.ZoomRectangle);
        }

        [TestMethod]
        public void InsertAt_CentersDefaultShapeAtClientPointInWorldCoordinates()
        {
            BaseController controller = CreateController(600, 400);
            controller.Canvas.UpdateScrollbars(new Rectangle(0, 0, 1200, 900), 100);
            controller.Canvas.SetViewportOrigin(40, 30);
            controller.SetZoom(200);

            GraphicElement inserted = controller.InsertAt(new Box(controller.Canvas), new Point(160, 130));

            Assert.AreEqual(new Rectangle(70, 50, 60, 60), inserted.DisplayRectangle);
        }

        [TestMethod]
        public void CustomConnectionPoints_RemainRelativeWhenShapeIsResized()
        {
            BaseController controller = CreateController(600, 400);
            Box box = AddBox(controller, new Rectangle(10, 20, 100, 80));
            box.SetCustomConnectionPoints(new[]
            {
                new ConnectionPoint(GripType.Center, new Point(5000, 2500))
            });

            Assert.AreEqual(new Point(60, 40), box.GetCustomConnectionPoints().Single().Point);

            box.DisplayRectangle = new Rectangle(10, 20, 200, 160);

            Assert.AreEqual(new Point(110, 60), box.GetCustomConnectionPoints().Single().Point);
        }

        [TestMethod]
        public void LineCaps_SupportSquareAndRoundCaps()
        {
            BaseController controller = CreateController(600, 400);
            HorizontalLine line = new HorizontalLine(controller.Canvas)
            {
                StartCap = AvailableLineCap.Square,
                EndCap = AvailableLineCap.Round
            };

            line.UpdateProperties();

            Assert.AreEqual(LineCap.SquareAnchor, line.BorderPen.StartCap);
            Assert.AreEqual(LineCap.RoundAnchor, line.BorderPen.EndCap);
        }

        [TestMethod]
        public void Persist_RoundTripsRotationWordWrapAndCustomConnectionPoints()
        {
            BaseController sourceController = CreateController(600, 400);
            Box source = AddBox(sourceController, new Rectangle(10, 20, 100, 80));
            source.RotationAngle = 45;
            source.WordWrap = false;
            source.SetCustomConnectionPoints(new[]
            {
                new ConnectionPoint(GripType.Center, new Point(5000, 5000))
            });

            string xml = Persist.Serialize(new List<GraphicElement> { source });
            BaseController targetController = CreateController(600, 400);
            GraphicElement target = Persist.Deserialize(targetController.Canvas, xml).Single();

            Assert.AreEqual(45, target.RotationAngle);
            Assert.IsFalse(target.WordWrap);
            Assert.AreEqual(new Point(5000, 5000), target.CustomConnectionPoints.Single().Point);
        }

        [TestMethod]
        public void SnapDelta_UsesCentersAndEdgesWithinRange()
        {
            BaseController controller = CreateController(600, 400);
            AddBox(controller, new Rectangle(100, 100, 50, 50));
            Box moving = AddBox(controller, new Rectangle(153, 200, 30, 30));

            Point delta = controller.GetCenterEdgeSnapDelta(moving, 5);

            Assert.AreEqual(new Point(-3, 0), delta);
        }

        [TestMethod]
        public void AlignSelected_AlignsToOutermostSelectedEdge()
        {
            BaseController controller = CreateController(600, 400);
            Box left = AddBox(controller, new Rectangle(100, 100, 50, 50));
            Box right = AddBox(controller, new Rectangle(180, 120, 40, 40));
            controller.SelectElement(left);
            controller.SelectElement(right);

            controller.AlignSelected(GripType.LeftMiddle);

            Assert.AreEqual(100, left.DisplayRectangle.Left);
            Assert.AreEqual(100, right.DisplayRectangle.Left);
        }

        [TestMethod]
        public void RotateSelected_SnapsAndNormalizesRotationAngle()
        {
            BaseController controller = CreateController(600, 400);
            Box box = AddBox(controller, new Rectangle(100, 100, 50, 50));
            controller.SelectElement(box);

            controller.RotateSelected(23);
            controller.RotateSelected(-60);

            Assert.AreEqual(330, box.RotationAngle);
        }

        [TestMethod]
        public void RotatedShape_RendersOutsideOriginalUnrotatedBounds()
        {
            BaseController controller = CreateController(260, 220);
            Box box = AddBox(controller, new Rectangle(100, 100, 80, 40));
            box.FillBrush.Color = Color.Black;
            box.BorderPen.Color = Color.Black;
            box.RotationAngle = 45;
            box.UpdatePath();

            using Bitmap bitmap = new Bitmap(260, 220);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.White);
            box.Draw(graphics, false);

            Rectangle bounds = FindNonWhiteBounds(bitmap);

            Assert.IsTrue(bounds.Top < box.ZoomRectangle.Top, "Rotated rendering should extend above the unrotated rectangle.");
        }

        [TestMethod]
        public void RenderTo_IsIndependentOfCanvasViewportAndZoom()
        {
            BaseController unscrolled = CreateController(600, 400);
            Box first = AddBox(unscrolled, new Rectangle(100, 80, 50, 40));
            first.FillBrush.Color = Color.Black;
            first.BorderPen.Color = Color.Black;

            BaseController scrolled = CreateController(600, 400);
            Box second = AddBox(scrolled, new Rectangle(100, 80, 50, 40));
            second.FillBrush.Color = Color.Black;
            second.BorderPen.Color = Color.Black;
            scrolled.Canvas.UpdateScrollbars(new Rectangle(0, 0, 1200, 900), 100);
            scrolled.Canvas.SetViewportOrigin(250, 170);
            scrolled.SetZoom(200);

            Rectangle firstBounds = RenderBounds(unscrolled);
            Rectangle secondBounds = RenderBounds(scrolled);

            Assert.AreEqual(firstBounds, secondBounds);
        }

        [TestMethod]
        public void RenderTo_DrawsDynamicConnectorsToTheTargetGraphics()
        {
            BaseController controller = CreateController(600, 400);
            DynamicConnectorLR connector = new DynamicConnectorLR(controller.Canvas, new Point(20, 20), new Point(160, 80));
            connector.BorderPen.Color = Color.Black;
            connector.UpdatePath();
            controller.AddElement(connector);

            Rectangle bounds = RenderBounds(controller);

            Assert.AreNotEqual(Rectangle.Empty, bounds);
            Assert.IsTrue(bounds.Right > 200);
            Assert.IsTrue(bounds.Bottom > 80);
        }

        [TestMethod]
        public void RenderTo_DrawsRotatedShapeGeometry()
        {
            BaseController controller = CreateController(600, 400);
            Box box = AddBox(controller, new Rectangle(100, 100, 80, 40));
            box.FillBrush.Color = Color.Black;
            box.BorderPen.Color = Color.Black;
            box.RotationAngle = 45;
            box.UpdatePath();

            Rectangle bounds = RenderBounds(controller);

            Assert.AreNotEqual(Rectangle.Empty, bounds);
            Assert.IsTrue(bounds.Top < 10, "Rotated print/export rendering should extend above the unrotated target bounds.");
        }

        [TestMethod]
        public void FocusOn_PansViewportToSelectedElement()
        {
            BaseController controller = CreateController(600, 400);
            Box box = AddBox(controller, new Rectangle(500, 400, 50, 50));
            controller.UpdateViewport();

            controller.FocusOn(box);

            Assert.AreEqual(new Point(225, 225), controller.Canvas.ViewportOrigin);
        }

        [TestMethod]
        public void CreatePrintDocument_ReturnsSinglePageDiagramDocument()
        {
            BaseController controller = CreateController(600, 400);
            AddBox(controller, new Rectangle(100, 100, 50, 50));

            using var document = controller.CreatePrintDocument();

            Assert.IsNotNull(document);
            Assert.AreEqual("document", document.DocumentName);
        }

        [TestMethod]
        public void AutoAnchor_ChoosesNearestConnectionPointsOnConnectedShapes()
        {
            BaseController controller = CreateController(600, 400);
            Box left = AddBox(controller, new Rectangle(10, 100, 50, 50));
            Box right = AddBox(controller, new Rectangle(200, 110, 50, 50));
            DynamicConnectorLR connector = new DynamicConnectorLR(controller.Canvas, new Point(0, 0), new Point(300, 300))
            {
                StartConnectedShape = left,
                EndConnectedShape = right
            };

            connector.AutoAnchor();

            Assert.AreEqual(left.DisplayRectangle.RightMiddle(), connector.StartPoint);
            Assert.AreEqual(right.DisplayRectangle.LeftMiddle(), connector.EndPoint);
        }

        [TestMethod]
        public void RegroupShapes_RestoresGroupMembershipAfterUngroup()
        {
            BaseController controller = CreateController(600, 400);
            Box first = AddBox(controller, new Rectangle(10, 20, 50, 50));
            Box second = AddBox(controller, new Rectangle(80, 20, 50, 50));
            controller.SelectElement(first);
            controller.SelectElement(second);
            GroupBox group = controller.GroupShapes(new GroupBox(controller.Canvas));

            controller.UngroupShapes(group, false);
            first.Move(new Point(10, 0));
            GroupBox regrouped = controller.RegroupShapes(group, new GraphicElement[] { first, second });

            Assert.AreSame(group, regrouped);
            Assert.AreSame(group, first.Parent);
            Assert.AreSame(group, second.Parent);
            Assert.IsTrue(group.GroupChildren.Contains(first));
            Assert.IsTrue(group.GroupChildren.Contains(second));
        }

        private static BaseController CreateController(int width, int height)
        {
            Canvas canvas = new Canvas
            {
                Size = new Size(width, height)
            };
            canvas.CreateBitmap(width, height);

            return new CanvasController(canvas);
        }

        private static Box AddBox(BaseController controller, Rectangle rectangle)
        {
            Box box = new Box(controller.Canvas)
            {
                DisplayRectangle = rectangle
            };
            box.UpdatePath();
            controller.AddElement(box);

            return box;
        }

        private static Rectangle RenderBounds(BaseController controller)
        {
            using Bitmap bitmap = new Bitmap(300, 220);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.White);
            controller.RenderTo(graphics, new Rectangle(10, 10, 260, 180));

            return FindNonWhiteBounds(bitmap);
        }

        private static Rectangle FindNonWhiteBounds(Bitmap bitmap)
        {
            int minX = bitmap.Width;
            int minY = bitmap.Height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).ToArgb() == Color.White.ToArgb())
                    {
                        continue;
                    }

                    minX = minX < x ? minX : x;
                    minY = minY < y ? minY : y;
                    maxX = maxX > x ? maxX : x;
                    maxY = maxY > y ? maxY : y;
                }
            }

            return maxX < 0 ? Rectangle.Empty : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        }
    }
}
