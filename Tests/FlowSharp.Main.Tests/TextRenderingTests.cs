using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpLib;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class TextRenderingTests
    {
        [TestMethod]
        public void DrawText_WithTopAndBottomAlignment_RendersInDifferentVerticalBands()
        {
            Rectangle topBounds = RenderTextBounds(ContentAlignment.TopCenter);
            Rectangle bottomBounds = RenderTextBounds(ContentAlignment.BottomCenter);

            Assert.AreNotEqual(Rectangle.Empty, topBounds);
            Assert.AreNotEqual(Rectangle.Empty, bottomBounds);
            Assert.IsTrue(topBounds.Top < bottomBounds.Top, "Top-aligned text should render above bottom-aligned text.");
            Assert.IsTrue(topBounds.Bottom + 20 < bottomBounds.Bottom, "Bottom-aligned text should render noticeably lower than top-aligned text.");
        }

        [TestMethod]
        public void Persist_RoundTripsTextAlign()
        {
            Canvas sourceCanvas = CreateCanvas();
            Box source = new Box(sourceCanvas)
            {
                Text = "Persist me",
                TextAlign = ContentAlignment.BottomRight
            };
            source.DisplayRectangle = new Rectangle(10, 10, 180, 120);
            source.UpdateZoomRectangle();

            string xml = Persist.Serialize(new List<GraphicElement> { source });

            Canvas targetCanvas = CreateCanvas();
            List<GraphicElement> elements = Persist.Deserialize(targetCanvas, xml);

            Assert.AreEqual(1, elements.Count);
            Assert.IsInstanceOfType(elements[0], typeof(Box));
            Assert.AreEqual(ContentAlignment.BottomRight, elements[0].TextAlign);
        }

        private static Rectangle RenderTextBounds(ContentAlignment alignment)
        {
            Canvas canvas = CreateCanvas();
            Box shape = new Box(canvas)
            {
                Text = "FlowSharp",
                TextAlign = alignment,
                TextColor = Color.Black
            };
            shape.BorderPen.Color = Color.White;
            shape.FillBrush.Color = Color.White;
            shape.TextFont.Dispose();
            shape.TextFont = new Font(FontFamily.GenericSansSerif, 24, FontStyle.Regular);
            shape.DisplayRectangle = new Rectangle(10, 10, 180, 120);
            shape.UpdateZoomRectangle();

            using (Bitmap bitmap = new Bitmap(220, 160))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                shape.Draw(graphics, false);
                shape.DrawText(graphics);
                return FindNonWhiteBounds(bitmap);
            }
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
                    Color pixel = bitmap.GetPixel(x, y);

                    if (pixel.ToArgb() == Color.White.ToArgb())
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

        private static Canvas CreateCanvas()
        {
            Canvas canvas = new Canvas();
            canvas.CreateBitmap(220, 160);
            _ = new CanvasController(canvas);

            return canvas;
        }
    }
}
