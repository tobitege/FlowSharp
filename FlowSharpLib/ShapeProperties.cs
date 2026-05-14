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
        public string Text { get; set; }
        [Category("Text")]
        public Font Font { get; set; }
        [Category("Text")]
        public Color TextColor { get; set; }
        [Category("Text")]
        public ContentAlignment TextAlign { get; set; }
        [Category("Text")]
        public bool Multiline { get; set; }
        [Category("Text")]
        public bool WordWrap { get; set; }
        [Category("Text")]
        public Rectangle TextBounds { get; set; }
        [Category("Text")]
        public int TextMargin { get; set; }
        [Category("Text")]
        public ParagraphJustification ParagraphJustification { get; set; }
        [Category("Geometry")]
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
