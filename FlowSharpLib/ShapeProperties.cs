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
    public class ShapeProperties : ElementProperties
    {
        [Category("Text")]
        [DisplayName("Text")]
        [Description("Text displayed inside the shape.")]
        public string Text { get; set; }
        [Category("Text")]
        [DisplayName("Font")]
        [Description("Font used to render the shape text.")]
        public Font Font { get; set; }
        [Category("Text")]
        [DisplayName("Text Color")]
        [Description("Color used to render the shape text.")]
        public Color TextColor { get; set; }
        [Category("Text")]
        [DisplayName("Text Alignment")]
        [Description("Horizontal and vertical alignment for text inside the text bounds.")]
        public ContentAlignment TextAlign { get; set; }
        [Category("Text")]
        [DisplayName("Multiline")]
        [Description("Allow text to render across multiple lines.")]
        public bool Multiline { get; set; }
        [Category("Text")]
        [DisplayName("Word Wrap")]
        [Description("Wrap text at word boundaries when multiline text is enabled.")]
        public bool WordWrap { get; set; }
        [Category("Text")]
        [DisplayName("Text Bounds")]
        [Description("Optional local text rectangle inside the shape. Leave empty to use the full shape bounds.")]
        public Rectangle TextBounds { get; set; }
        [Category("Text")]
        [DisplayName("Text Margin")]
        [Description("Inner text padding in pixels.")]
        public int TextMargin { get; set; }
        [Category("Text")]
        [DisplayName("Paragraph Justification")]
        [Description("Paragraph alignment behavior for wrapped text.")]
        public ParagraphJustification ParagraphJustification { get; set; }
        [Category("Geometry")]
        [DisplayName("Rotation Angle")]
        [Description("Clockwise shape rotation angle in degrees.")]
        public int RotationAngle { get; set; }

        public ShapeProperties(GraphicElement el) : base(el)
        {
            Text = el.Text;
            Font = el.TextFont;
            TextColor = el.TextColor;
            TextAlign = el.TextAlign;
            Multiline = el.Multiline;
            WordWrap = el.WordWrap;
            TextBounds = el.TextBounds;
            TextMargin = el.TextMargin;
            ParagraphJustification = el.ParagraphJustification;
            RotationAngle = el.RotationAngle;
        }

        public override PropertyRedrawMode GetRedrawMode(string label)
        {
            switch (label)
            {
                case nameof(RotationAngle):
                    return PropertyRedrawMode.ElementAndConnections;

                default:
                    return base.GetRedrawMode(label);
            }
        }

        public override void Update(GraphicElement el, string label)
        {
            // X1
            //(label == nameof(Text)).If(() => this.ChangePropertyWithUndoRedo<string>(el, nameof(el.Text), nameof(Text)));
            //(label == nameof(Font)).If(() => this.ChangePropertyWithUndoRedo<Font>(el, nameof(el.TextFont), nameof(Font)));
            //(label == nameof(TextColor)).If(() => this.ChangePropertyWithUndoRedo<Color>(el, nameof(el.TextColor), nameof(TextColor)));
            //(label == nameof(TextAlign)).If(() => this.ChangePropertyWithUndoRedo<Color>(el, nameof(el.TextAlign), nameof(TextAlign)));
            (label == nameof(Text)).If(() => el.Text = Text);
            (label == nameof(Font)).If(() => el.TextFont = Font);
            (label == nameof(TextColor)).If(() => el.TextColor = TextColor);
            (label == nameof(TextAlign)).If(() => el.TextAlign = TextAlign);
            (label == nameof(Multiline)).If(() => el.Multiline = Multiline);
            (label == nameof(WordWrap)).If(() => el.WordWrap = WordWrap);
            (label == nameof(TextBounds)).If(() => el.TextBounds = TextBounds);
            (label == nameof(TextMargin)).If(() => el.TextMargin = TextMargin);
            (label == nameof(ParagraphJustification)).If(() => el.ParagraphJustification = ParagraphJustification);
            (label == nameof(RotationAngle)).If(() => el.RotationAngle = RotationAngle);
            base.Update(el, label);
        }
    }
}
