/* 
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System.ComponentModel;
using System.Drawing;

using Clifton.Core.ExtensionMethods;

namespace FlowSharpLib
{
    public enum PropertyRedrawMode
    {
        None,
        Element,
        ElementAndConnections
    }

	public abstract class ElementProperties : IPropertyObject
	{
        protected GraphicElement element;

        [Category("Element")]
        [DisplayName("Name")]
        [Description("Optional diagram element name used by commands and runtime inspection.")]
        public string Name { get; set; }
        [Category("Element")]
        [DisplayName("Shape Type")]
        [Description("The concrete FlowSharp shape or connector type.")]
        [ReadOnly(true)]
        public string ShapeName => element?.GetType().Name;
        [Category("Element")]
        [DisplayName("Bounds")]
        [Description("The element position and size in diagram coordinates.")]
		public Rectangle Rectangle { get; set; }

		[Category("Border")]
        [DisplayName("Border Color")]
        [Description("The outline color used when drawing the element.")]
		public Color BorderColor { get; set; }
        [Category("Border")]
        [DisplayName("Border Width")]
        [Description("The outline width in pixels.")]
		public int BorderWidth { get; set; }

		[Category("Fill")]
        [DisplayName("Fill Color")]
        [Description("The interior fill color used when drawing the element.")]
		public Color FillColor { get; set; }

		public ElementProperties(GraphicElement el)
		{
			this.element = el;
			Rectangle = el.DisplayRectangle;
			BorderColor = el.BorderPen.Color;
			BorderWidth = (int)el.BorderPen.Width;
			FillColor = el.FillBrush.Color;
            Name = el.Name;
		}

		public virtual void UpdateFrom(GraphicElement el)
		{
			// The only property that can change.
			Rectangle = el.DisplayRectangle;
		}

        public virtual PropertyRedrawMode GetRedrawMode(string label)
        {
            switch (label)
            {
                case nameof(Name):
                case nameof(ShapeName):
                    return PropertyRedrawMode.None;

                case nameof(Rectangle):
                    return PropertyRedrawMode.ElementAndConnections;

                default:
                    return PropertyRedrawMode.Element;
            }
        }

		public virtual void Update(GraphicElement el, string label)
		{
            // X1
            //(label == nameof(Rectangle)).If(() => this.ChangePropertyWithUndoRedo<Rectangle>(el, nameof(el.DisplayRectangle), nameof(Rectangle)));
            //(label == nameof(BorderColor)).If(() => this.ChangePropertyWithUndoRedo<Color>(el, nameof(el.BorderPenColor), nameof(BorderColor)));
            //(label == nameof(BorderWidth)).If(() => this.ChangePropertyWithUndoRedo<int>(el, nameof(el.BorderPenWidth), nameof(BorderWidth)));
            //(label == nameof(FillColor)).If(() => this.ChangePropertyWithUndoRedo<Color>(el, nameof(el.FillColor), nameof(FillColor)));
            (label == nameof(Rectangle)).If(() => el.DisplayRectangle = Rectangle);
            (label == nameof(BorderColor)).If(() => el.BorderPenColor = BorderColor);
            (label == nameof(BorderWidth)).If(() => el.BorderPenWidth = BorderWidth);
            (label == nameof(FillColor)).If(() => el.FillColor = FillColor);
            (label == nameof(Name)).If(() => el.Name = Name);
        }

        public virtual void Update(string label) { }
    }
}
