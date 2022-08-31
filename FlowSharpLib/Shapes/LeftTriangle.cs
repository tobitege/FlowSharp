/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FlowSharpLib
{
    [ToolboxOrder(4)]
    public class LeftTriangle : GraphicElement
    {
        protected Point[] path;

        public LeftTriangle(Canvas canvas) : base(canvas)
        {
        }

        public override List<ConnectionPoint> GetConnectionPoints()
        {
            return new List<ConnectionPoint>
            {
                new ConnectionPoint(GripType.LeftMiddle,    ZoomRectangle.LeftMiddle()),
                new ConnectionPoint(GripType.RightMiddle,   ZoomRectangle.RightMiddle()),
                new ConnectionPoint(GripType.TopRight,      ZoomRectangle.TopRightCorner()),
                new ConnectionPoint(GripType.BottomRight,   ZoomRectangle.BottomRightCorner())
            };
        }

        public override void UpdatePath()
        {
            path = new Point[]
            {
                new Point(ZoomRectangle.X,                       ZoomRectangle.Y + ZoomRectangle.Height/2), // left, middle
                new Point(ZoomRectangle.X + ZoomRectangle.Width, ZoomRectangle.Y),                          // right, top
                new Point(ZoomRectangle.X + ZoomRectangle.Width, ZoomRectangle.Y + ZoomRectangle.Height),   // right, bottom
                new Point(ZoomRectangle.X,                       ZoomRectangle.Y + ZoomRectangle.Height/2), // left, middle
            };
        }

        protected Point[] ZPath()
        {
            var r = ZoomRectangle;
            r.X = 0;
            r.Y = 0;
            var adjust = (int)((BorderPen.Width + 0) / 2);
            return new Point[]
            {
                new Point(r.X + adjust,           r.Y + r.Height/2),
                new Point(r.X + r.Width - adjust, r.Y + adjust),
                new Point(r.X + r.Width - adjust, r.Y + r.Height - adjust),
                new Point(r.X + adjust,           r.Y + r.Height/2),
            };
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
