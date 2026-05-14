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
	public class LineProperties : ElementProperties
	{
		[Category("Endcaps")]
        [DisplayName("Start Cap")]
        [Description("Decoration rendered at the connector start point.")]
		public AvailableLineCap StartCap { get; set; }
		[Category("Endcaps")]
        [DisplayName("End Cap")]
        [Description("Decoration rendered at the connector end point.")]
		public AvailableLineCap EndCap { get; set; }
        [Category("Label")]
        [DisplayName("Label Offset")]
        [Description("Label position offset from the connector midpoint.")]
        public Point LabelOffset { get; set; }
        [Category("Label")]
        [DisplayName("Label Size")]
        [Description("Editable connector label rectangle size.")]
        public Size LabelSize { get; set; }

		public LineProperties(Line el) : base(el)
		{
			StartCap = el.StartCap;
			EndCap = el.EndCap;
            LabelOffset = el.LabelOffset;
            LabelSize = el.LabelSize;
		}

        public override PropertyRedrawMode GetRedrawMode(string label)
        {
            switch (label)
            {
                case nameof(StartCap):
                case nameof(EndCap):
                case nameof(LabelOffset):
                case nameof(LabelSize):
                    return PropertyRedrawMode.Element;

                default:
                    return base.GetRedrawMode(label);
            }
        }

		public override void Update(GraphicElement el, string label)
		{
            // X1
            //(label == nameof(StartCap)).If(() => this.ChangePropertyWithUndoRedo<AvailableLineCap>(el, nameof(StartCap), nameof(StartCap)));
            //(label == nameof(EndCap)).If(() => this.ChangePropertyWithUndoRedo<AvailableLineCap>(el, nameof(EndCap), nameof(EndCap)));
            (label == nameof(StartCap)).If(() => ((Connector)el).StartCap = StartCap);
            (label == nameof(EndCap)).If(() => ((Connector)el).EndCap = EndCap);
            (label == nameof(LabelOffset)).If(() => ((Connector)el).LabelOffset = LabelOffset);
            (label == nameof(LabelSize)).If(() => ((Connector)el).LabelSize = LabelSize);
            base.Update(el, label);
        }
    }
}
