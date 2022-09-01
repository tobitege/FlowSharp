/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

// ReSharper disable once CheckNamespace
namespace FlowSharpLib
{
    [ToolboxOrder(6)]
    public class UpTriangle : GraphicElement
    {
        protected Point[] path;

        public UpTriangle(Canvas canvas) : base(canvas)
        {
        }

        public override List<ConnectionPoint> GetConnectionPoints()
        {
            var connectionPoints = new List<ConnectionPoint>
            {
                new ConnectionPoint(GripType.TopMiddle, ZoomRectangle.TopMiddle()),
                new ConnectionPoint(GripType.BottomMiddle, ZoomRectangle.BottomMiddle()),
                new ConnectionPoint(GripType.BottomLeft, ZoomRectangle.BottomLeftCorner()),
                new ConnectionPoint(GripType.BottomRight, ZoomRectangle.BottomRightCorner())
            };

            return connectionPoints;
        }

        public override void UpdatePath()
        {
            path = new[]
            {
                new Point(ZoomRectangle.X + ZoomRectangle.Width/2,        ZoomRectangle.Y),        // middle, top
                new Point(ZoomRectangle.X + ZoomRectangle.Width,          ZoomRectangle.Y + ZoomRectangle.Height), // right, bottom
                new Point(ZoomRectangle.X,          ZoomRectangle.Y + ZoomRectangle.Height),       // left, bottom
                new Point(ZoomRectangle.X + ZoomRectangle.Width/2,        ZoomRectangle.Y),        // middle, Top
            };
        }

        protected Point[] ZPath()
        {
            var r = ZoomRectangle;
            r.X = 0;
            r.Y = 0;
            var adjust = (int)((BorderPen.Width + 0) / 2);
            var p = new[]
            {
                new Point(r.X + r.Width/2, r.Y + adjust),                             // right, middle
                new Point(r.X + r.Width - adjust,           r.Y + r.Height - adjust), // left, top
                new Point(r.X + adjust,           r.Y + r.Height - adjust),           // left, bottom
                new Point(r.X + r.Width/2, r.Y + adjust),                             // right, middle
            };
            return p;
        }

        public override void Draw(Graphics gr, bool showSelection = true)
        {
            var r = ZoomRectangle.Grow(2);
            var bitmap = new Bitmap(r.Width, r.Height);
            var g2 = Graphics.FromImage(bitmap);
            g2.SmoothingMode = SmoothingMode.AntiAlias;
            var p = ZPath();
            g2.FillPolygon(FillBrush, p);
            g2.DrawPolygon(BorderPen, p);
            gr.DrawImage(bitmap, ZoomRectangle.X, ZoomRectangle.Y);
            bitmap.Dispose();
            g2.Dispose();
            base.Draw(gr, showSelection);
        }
    }
}
