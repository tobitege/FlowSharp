/* 
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

using Clifton.Core.ExtensionMethods;

namespace FlowSharpLib
{
    public class CanvasProperties : IPropertyObject
    {
        [Category("Canvas")]
        public string Name { get; set; }
        [Category("Canvas")]
        public string Filename { get; set; }
        [Category("Page")]
        public Rectangle PageBounds { get; set; }
        [Category("Page")]
        public Padding PageMargins { get; set; }
        [Category("Page")]
        public bool ShowPageBounds { get; set; }

        protected Canvas canvas;

        public CanvasProperties(Canvas canvas)
        {
            this.canvas = canvas;
            Name = canvas.Controller.CanvasName;
            Filename= canvas.Controller.Filename;
            PageBounds = canvas.PageBounds;
            PageMargins = canvas.PageMargins;
            ShowPageBounds = canvas.ShowPageBounds;
        }

        public void Update(string label)
        {
            (label == nameof(Name)).If(() => canvas.Controller.CanvasName = Name);
            (label == nameof(Filename)).If(() => canvas.Controller.Filename = Filename);
            (label == nameof(PageBounds)).If(() => canvas.PageBounds = PageBounds);
            (label == nameof(PageMargins)).If(() => canvas.PageMargins = PageMargins);
            (label == nameof(ShowPageBounds)).If(() => canvas.ShowPageBounds = ShowPageBounds);
            canvas.Controller.UpdateViewport();
            canvas.Invalidate();
        }
    }
}
