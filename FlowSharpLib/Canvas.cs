/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Drawing;
using System.Windows.Forms;

using Clifton.Core.ExtensionMethods;
using Clifton.Core.ServiceManagement;

namespace FlowSharpLib
{
    public class Canvas : Panel
    {
        private const int ResizeRefreshIntervalMs = 33;     // ~30 FPS during drag/resize.
        private const int BitmapGrowthPadding = 128;
        private const int ScrollbarThickness = 17;

        public IServiceManager ServiceManager { get; set; }
        public Action<Canvas> PaintComplete { get; set; }
        public Color BackgroundColor => canvasBrush.Color;
        public BaseController Controller { get; set; }
        public Bitmap Bitmap => bitmap;
        public Point ViewportOrigin => viewportOrigin;
        public Size VirtualSize => virtualSize;
        public Rectangle PageBounds { get; set; }
        public Padding PageMargins { get; set; }
        public bool ShowPageBounds { get; set; }

        protected SolidBrush canvasBrush;
        protected Pen gridPen;
        protected Pen pagePen;
        protected Pen marginPen;
        protected Size gridSpacing;
        protected Bitmap bitmap;
        protected Point origin = new Point(0, 0);
        protected Point viewportOrigin = new Point(0, 0);
        protected Size virtualSize = Size.Empty;
        protected Point dragOffset = new Point(0, 0);

        protected Graphics graphics;
        protected Graphics antiAliasGraphics;
        protected Timer resizeRefreshTimer;
        protected bool resizeRefreshPending;
        protected HScrollBar horizontalScrollBar;
        protected VScrollBar verticalScrollBar;
        protected Control parentControl;

        public Graphics Graphics => graphics;
        public Graphics AntiAliasGraphics => antiAliasGraphics;

        public Canvas()
        {
            DoubleBuffered = true;
            canvasBrush = new SolidBrush(Color.White);
            gridPen = new Pen(Color.LightBlue);
            pagePen = new Pen(Color.Gray);
            marginPen = new Pen(Color.LightGray) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            gridSpacing = new Size(32, 32);
            PageBounds = new Rectangle(0, 0, 850, 1100);
            PageMargins = new Padding(50);
            ShowPageBounds = false;
        }

        public void EndInit()
        {
            Paint += OnPaint;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                if (parentControl != null)
                {
                    parentControl.Resize -= OnParentResize;
                    parentControl = null;
                }

                if (resizeRefreshTimer != null)
                {
                    resizeRefreshTimer.Stop();
                    resizeRefreshTimer.Tick -= OnResizeRefreshTimerTick;
                    resizeRefreshTimer.Dispose();
                    resizeRefreshTimer = null;
                }

                graphics?.Dispose();
                antiAliasGraphics?.Dispose();
                canvasBrush?.Dispose();
                gridPen?.Dispose();
                pagePen?.Dispose();
                marginPen?.Dispose();
                bitmap?.Dispose();
            }
        }

        /// <summary>
        /// Canvas.Initialize requires that the parent be attached to the form!
        /// </summary>
        /// <param name="parent"></param>
        public void Initialize(Control parent)
        {
            Dock = DockStyle.Fill;
            parent.Controls.Add(this);
            parentControl = parent;
            InitializeScrollbars();

            if (NotMinimized())
            {
                CreateBitmap();
            }

            resizeRefreshTimer = new Timer
            {
                Interval = ResizeRefreshIntervalMs
            };
            resizeRefreshTimer.Tick += OnResizeRefreshTimerTick;
            parent.Resize += OnParentResize;
        }

        public void DrawImage(Bitmap img, Rectangle r)
        {
            Graphics.DrawImage(img, r);
        }

        public Bitmap GetImage(Rectangle r)
        {
            return bitmap.Clone(r, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        }

        public void CopyToScreen(Rectangle r)
        {
            Bitmap b = bitmap.Clone(r, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics grScreen = CreateGraphics();
            grScreen.DrawImage(b, r);
            b.Dispose();
            grScreen.Dispose();
        }

        public bool OnScreen(Rectangle r)
        {
            return r.X < bitmap.Width && r.Y < bitmap.Height && r.Location.X + r.Width >= 0 && r.Location.Y + r.Height >= 0 && r.Width > 0 && r.Height > 0;
        }

        public void Drag(Point p)
        {
            SetViewportOrigin(viewportOrigin.X - p.X, viewportOrigin.Y - p.Y);
        }

        public Point WorldToClient(Point worldPoint, int zoom)
        {
            return new Point(
                worldPoint.X * zoom / 100 - viewportOrigin.X,
                worldPoint.Y * zoom / 100 - viewportOrigin.Y);
        }

        public Point ClientToWorld(Point clientPoint, int zoom)
        {
            return new Point(
                (clientPoint.X + viewportOrigin.X) * 100 / zoom,
                (clientPoint.Y + viewportOrigin.Y) * 100 / zoom);
        }

        public Rectangle WorldToClient(Rectangle worldRectangle, int zoom)
        {
            Point location = WorldToClient(worldRectangle.Location, zoom);
            Size size = new Size(worldRectangle.Width * zoom / 100, worldRectangle.Height * zoom / 100);

            return new Rectangle(location, size);
        }

        public void SetViewportOrigin(Point origin)
        {
            SetViewportOrigin(origin.X, origin.Y);
        }

        public void SetViewportOrigin(int x, int y)
        {
            var maxX = Math.Max(0, virtualSize.Width - ViewWidth);
            var maxY = Math.Max(0, virtualSize.Height - ViewHeight);
            var next = new Point(Math.Max(0, Math.Min(x, maxX)), Math.Max(0, Math.Min(y, maxY)));

            if (next == viewportOrigin)
            {
                return;
            }

            viewportOrigin = next;
            dragOffset = new Point(-viewportOrigin.X % gridSpacing.Width, -viewportOrigin.Y % gridSpacing.Height);
            UpdateScrollbarValues();
            Controller?.UpdateViewport();
            Invalidate();
        }

        public void UseViewportOrigin(Point origin, Action action)
        {
            Point previous = viewportOrigin;
            viewportOrigin = origin;

            try
            {
                action();
            }
            finally
            {
                viewportOrigin = previous;
            }
        }

        public void UpdateScrollbars(Rectangle worldExtents, int zoom)
        {
            var scaledExtents = new Rectangle(
                worldExtents.X * zoom / 100,
                worldExtents.Y * zoom / 100,
                worldExtents.Width * zoom / 100,
                worldExtents.Height * zoom / 100);
            int width = Math.Max(ViewWidth, scaledExtents.Right + gridSpacing.Width);
            int height = Math.Max(ViewHeight, scaledExtents.Bottom + gridSpacing.Height);
            virtualSize = new Size(Math.Max(width, PageBounds.Width * zoom / 100), Math.Max(height, PageBounds.Height * zoom / 100));
            ConfigureScrollbar(horizontalScrollBar, ViewWidth, virtualSize.Width);
            ConfigureScrollbar(verticalScrollBar, ViewHeight, virtualSize.Height);
            SetViewportOrigin(viewportOrigin);
        }

        public Rectangle Clip(Rectangle r)
        {
            int x = r.X.Max(0);
            int y = r.Y.Max(0);
            int width = (r.X + r.Width).Min(bitmap.Width) - r.X;
            int height = (r.Y + r.Height).Min(bitmap.Height) - r.Y;

            width += r.X - x;
            height += r.Y - y;

            return new Rectangle(x, y, width, height);
        }

        public void CreateBitmap(int w, int h)
        {
            bitmap?.Dispose();
            bitmap = new Bitmap(w, h);
            CreateGraphicsObjects();
        }

        protected bool NotMinimized()
        {
            return FindForm().WindowState != FormWindowState.Minimized && ClientSize.Width != 0 && ClientSize.Height != 0;
        }

        protected void CreateBitmap()
        {
            bitmap?.Dispose();
            bitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
            CreateGraphicsObjects();
        }

        protected void EnsureBitmapCapacity(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (bitmap == null)
            {
                CreateBitmap(width, height);
                return;
            }

            if (width <= bitmap.Width && height <= bitmap.Height)
            {
                return;
            }

            int targetWidth = Math.Max(width, bitmap.Width + BitmapGrowthPadding);
            int targetHeight = Math.Max(height, bitmap.Height + BitmapGrowthPadding);
            CreateBitmap(targetWidth, targetHeight);
        }

        protected void OnParentResize(object sender, EventArgs e)
        {
            if (!NotMinimized())
            {
                return;
            }

            resizeRefreshPending = true;

            if (!resizeRefreshTimer.Enabled)
            {
                resizeRefreshTimer.Start();
            }
        }

        protected void OnResizeRefreshTimerTick(object sender, EventArgs e)
        {
            if (!resizeRefreshPending)
            {
                resizeRefreshTimer.Stop();
                return;
            }

            resizeRefreshPending = false;

            if (NotMinimized())
            {
                EnsureBitmapCapacity(ClientSize.Width, ClientSize.Height);
                Controller?.UpdateViewport();
                Invalidate();
            }

            if (!resizeRefreshPending)
            {
                resizeRefreshTimer.Stop();
            }
        }

        protected void CreateGraphicsObjects()
        {
            graphics?.Dispose();
            graphics = Graphics.FromImage(bitmap);
            antiAliasGraphics = Graphics.FromImage(bitmap);
            antiAliasGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        }

        protected void OnPaint(object sender, PaintEventArgs e)
        {
            // Controller.OnPaint(e);
            // WinForm controls will cause an OnPaint when they are moved/redrawn, so
            // we ignore the paint when the canvas is being dragged.
            if (!Controller.IsCanvasDragging)
            {
                // Otherwise, draw only shapes that intersect with the clip rectangle.
                // elements.Where(el => el.UpdateRectangle.IntersectsWith(e.ClipRectangle)).ForEach(el =>
                {
                    // TODO: Right now, we're redrawing the whole surface.  Optimize this as per comments above.
                    Graphics gr = Graphics;
                    DrawBackground(gr);
                    DrawGrid(gr);
                    DrawPageBounds(gr);
                    PaintComplete(this);
                    e.Graphics.DrawImage(bitmap, origin);
                }
            }
        }

        protected int ViewWidth => Math.Max(0, ClientSize.Width - (verticalScrollBar?.Visible == true ? ScrollbarThickness : 0));
        protected int ViewHeight => Math.Max(0, ClientSize.Height - (horizontalScrollBar?.Visible == true ? ScrollbarThickness : 0));

        protected void InitializeScrollbars()
        {
            horizontalScrollBar = new HScrollBar
            {
                Dock = DockStyle.Bottom,
                Visible = false
            };
            verticalScrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                Visible = false
            };
            horizontalScrollBar.Scroll += (sender, args) => SetViewportOrigin(horizontalScrollBar.Value, viewportOrigin.Y);
            verticalScrollBar.Scroll += (sender, args) => SetViewportOrigin(viewportOrigin.X, verticalScrollBar.Value);
            Controls.Add(horizontalScrollBar);
            Controls.Add(verticalScrollBar);
        }

        protected void ConfigureScrollbar(ScrollBar scrollBar, int viewSize, int contentSize)
        {
            if (scrollBar == null)
            {
                return;
            }

            bool visible = contentSize > viewSize && viewSize > 0;
            scrollBar.Visible = visible;

            if (!visible)
            {
                scrollBar.Value = 0;
                return;
            }

            scrollBar.Minimum = 0;
            scrollBar.LargeChange = Math.Max(1, viewSize);
            scrollBar.SmallChange = Math.Max(1, viewSize / 10);
            scrollBar.Maximum = Math.Max(0, contentSize - 1);
        }

        protected void UpdateScrollbarValues()
        {
            SetScrollbarValue(horizontalScrollBar, viewportOrigin.X);
            SetScrollbarValue(verticalScrollBar, viewportOrigin.Y);
        }

        protected void SetScrollbarValue(ScrollBar scrollBar, int value)
        {
            if (scrollBar == null || !scrollBar.Visible)
            {
                return;
            }

            int maxValue = Math.Max(scrollBar.Minimum, scrollBar.Maximum - scrollBar.LargeChange + 1);
            scrollBar.Value = Math.Max(scrollBar.Minimum, Math.Min(value, maxValue));
        }

        protected virtual void DrawBackground(Graphics gr)
        {
            gr.Clear(canvasBrush.Color);
        }

        protected virtual void DrawGrid(Graphics gr)
        {
            DrawVerticalGridLines(gr);
            DrawHorizontalGridLines(gr);
        }

        public void DrawVerticalGridLines(Graphics gr)
        {
            DisplayRectangle.Height.Step2(gridSpacing.Height,
                ((y) =>
                    DisplayRectangle.Width.Step2(gridSpacing.Width, (x) =>
                        DrawLine(gr, gridPen, x+dragOffset.X, 0, x+dragOffset.X, DisplayRectangle.Height)
                )));
        }

        public void DrawHorizontalGridLines(Graphics gr)
        {
            DisplayRectangle.Width.Step2(gridSpacing.Width,
                ((x) =>
                    DisplayRectangle.Height.Step2(gridSpacing.Height, (y) =>
                        DrawLine(gr, gridPen, 0, y+dragOffset.Y, DisplayRectangle.Width, y+dragOffset.Y)
                )));
        }

        protected virtual void DrawPageBounds(Graphics gr)
        {
            if (!ShowPageBounds || Controller == null)
            {
                return;
            }

            Rectangle page = WorldToClient(PageBounds, Controller.Zoom);
            gr.DrawRectangle(pagePen, page);

            Rectangle margins = new Rectangle(
                PageBounds.Left + PageMargins.Left,
                PageBounds.Top + PageMargins.Top,
                PageBounds.Width - PageMargins.Left - PageMargins.Right,
                PageBounds.Height - PageMargins.Top - PageMargins.Bottom);
            gr.DrawRectangle(marginPen, WorldToClient(margins, Controller.Zoom));
        }

        protected void DrawLine(Graphics gr, Pen pen, int x1, int y1, int x2, int y2)
        {
            gr.DrawLine(pen, x1, y1, x2, y2);
        }
    }
}
