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
            source.TextBounds = new Rectangle(5, 6, 40, 30);
            source.TextMargin = 4;
            source.ParagraphJustification = ParagraphJustification.Justify;
            source.SetCustomConnectionPoints(new[]
            {
                new ConnectionPoint(GripType.Center, new Point(5000, 5000))
            });

            string xml = Persist.Serialize(new List<GraphicElement> { source });
            BaseController targetController = CreateController(600, 400);
            GraphicElement target = Persist.Deserialize(targetController.Canvas, xml).Single();

            Assert.AreEqual(45, target.RotationAngle);
            Assert.IsFalse(target.WordWrap);
            Assert.AreEqual(new Rectangle(5, 6, 40, 30), target.TextBounds);
            Assert.AreEqual(4, target.TextMargin);
            Assert.AreEqual(ParagraphJustification.Justify, target.ParagraphJustification);
            Assert.AreEqual(new Point(5000, 5000), target.CustomConnectionPoints.Single().Point);
        }

        [TestMethod]
        public void NewShapes_DefaultToWrappedMultilineTextLayout()
        {
            BaseController controller = CreateController(600, 400);
            Box box = AddBox(controller, new Rectangle(10, 20, 100, 80));

            Assert.IsTrue(box.Multiline);
            Assert.IsTrue(box.WordWrap);
            Assert.AreEqual(box.DisplayRectangle, box.GetTextDisplayRectangle());
        }

        [TestMethod]
        public void TextBounds_AreLocalToTheShapeDisplayRectangle()
        {
            BaseController controller = CreateController(600, 400);
            Box box = AddBox(controller, new Rectangle(10, 20, 100, 80));
            box.TextBounds = new Rectangle(5, 10, 40, 25);

            Assert.AreEqual(new Rectangle(15, 30, 40, 25), box.GetTextDisplayRectangle());

            box.Move(new Point(20, 30));

            Assert.AreEqual(new Rectangle(35, 60, 40, 25), box.GetTextDisplayRectangle());
        }

        [TestMethod]
        public void JustifiedText_RendersInsideCustomTextBounds()
        {
            BaseController controller = CreateController(260, 180);
            Box box = AddBox(controller, new Rectangle(10, 10, 220, 140));
            box.Text = "one two three four five six seven eight";
            box.TextAlign = ContentAlignment.TopLeft;
            box.TextBounds = new Rectangle(40, 35, 90, 60);
            box.ParagraphJustification = ParagraphJustification.Justify;

            using Bitmap bitmap = new Bitmap(260, 180);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.White);
            box.DrawText(graphics);

            Rectangle bounds = FindNonWhiteBounds(bitmap);
            Rectangle textBounds = box.GetTextDisplayRectangle().Grow(-box.TextMargin);

            Assert.AreNotEqual(Rectangle.Empty, bounds);
            Assert.IsTrue(bounds.Left >= textBounds.Left - 1);
            Assert.IsTrue(bounds.Top >= textBounds.Top - 1);
            Assert.IsTrue(bounds.Right <= textBounds.Right + 1);
            Assert.IsTrue(bounds.Bottom <= textBounds.Bottom + 1);
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
        public void ConnectorLabelRectangle_UsesMidpointOffsetAndSize()
        {
            BaseController controller = CreateController(600, 400);
            DynamicConnectorLR connector = new DynamicConnectorLR(controller.Canvas, new Point(10, 20), new Point(110, 80))
            {
                LabelOffset = new Point(20, -10),
                LabelSize = new Size(100, 24)
            };

            Assert.AreEqual(new Rectangle(30, 28, 100, 24), connector.GetLabelDisplayRectangle());
            Assert.AreEqual(connector.GetLabelDisplayRectangle(), connector.GetTextDisplayRectangle());
        }

        [TestMethod]
        public void ConnectorCreateTextBox_UsesLabelRectangle()
        {
            BaseController controller = CreateController(600, 400);
            DynamicConnectorLR connector = new DynamicConnectorLR(controller.Canvas, new Point(10, 20), new Point(110, 80))
            {
                Text = "connector label",
                LabelOffset = new Point(20, -10),
                LabelSize = new Size(100, 24)
            };

            using System.Windows.Forms.TextBox textBox = connector.CreateTextBox(Point.Empty);

            Assert.AreEqual(new Point(30, 28), textBox.Location);
            Assert.AreEqual(new Size(100, 24), textBox.Size);
            Assert.AreEqual("connector label", textBox.Text);
            Assert.IsTrue(textBox.Multiline);
            Assert.IsTrue(textBox.WordWrap);
        }

        [TestMethod]
        public void Persist_RoundTripsConnectorLabelLayout()
        {
            BaseController sourceController = CreateController(600, 400);
            DynamicConnectorLR source = new DynamicConnectorLR(sourceController.Canvas, new Point(10, 20), new Point(110, 80))
            {
                Text = "connector label",
                LabelOffset = new Point(12, -8),
                LabelSize = new Size(120, 26)
            };

            string xml = Persist.Serialize(new List<GraphicElement> { source });
            BaseController targetController = CreateController(600, 400);
            DynamicConnectorLR target = (DynamicConnectorLR)Persist.Deserialize(targetController.Canvas, xml).Single();

            Assert.AreEqual("connector label", target.Text);
            Assert.AreEqual(new Point(12, -8), target.LabelOffset);
            Assert.AreEqual(new Size(120, 26), target.LabelSize);
        }

        [TestMethod]
        public void PropertyRedrawPolicy_SkipsNonVisualNameChanges()
        {
            BaseController controller = CreateController(600, 400);
            Box box = AddBox(controller, new Rectangle(10, 20, 100, 80));
            ShapeProperties properties = new ShapeProperties(box);

            Assert.AreEqual(PropertyRedrawMode.None, properties.GetRedrawMode(nameof(ElementProperties.Name)));
        }

        [TestMethod]
        public void PropertyRedrawPolicy_UpdatesConnectionsForGeometryChanges()
        {
            BaseController controller = CreateController(600, 400);
            Box box = AddBox(controller, new Rectangle(10, 20, 100, 80));
            ShapeProperties properties = new ShapeProperties(box);

            Assert.AreEqual(PropertyRedrawMode.ElementAndConnections, properties.GetRedrawMode(nameof(ElementProperties.Rectangle)));
            Assert.AreEqual(PropertyRedrawMode.ElementAndConnections, properties.GetRedrawMode(nameof(ShapeProperties.RotationAngle)));
        }

        [TestMethod]
        public void PropertyRedrawPolicy_TargetsConnectorVisualProperties()
        {
            BaseController controller = CreateController(600, 400);
            DynamicConnectorLR connector = new DynamicConnectorLR(controller.Canvas, new Point(10, 20), new Point(110, 80));
            DynamicConnectorProperties properties = new DynamicConnectorProperties(connector);

            Assert.AreEqual(PropertyRedrawMode.Element, properties.GetRedrawMode(nameof(DynamicConnectorProperties.EndCap)));
            Assert.AreEqual(PropertyRedrawMode.Element, properties.GetRedrawMode(nameof(DynamicConnectorProperties.LabelOffset)));
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
