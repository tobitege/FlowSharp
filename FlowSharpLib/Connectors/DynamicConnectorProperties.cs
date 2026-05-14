/* 
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System.ComponentModel;

using Clifton.Core.ExtensionMethods;

namespace FlowSharpLib
{
	public class DynamicConnectorProperties : ElementProperties
	{
		[Category("Endcaps")]
		public AvailableLineCap StartCap { get; set; }
		[Category("Endcaps")]
		public AvailableLineCap EndCap { get; set; }
        [Category("Label")]
        public System.Drawing.Point LabelOffset { get; set; }
        [Category("Label")]
        public System.Drawing.Size LabelSize { get; set; }

		public DynamicConnectorProperties(DynamicConnector el) : base(el)
		{
			StartCap = el.StartCap;
			EndCap = el.EndCap;
            LabelOffset = el.LabelOffset;
            LabelSize = el.LabelSize;
		}

		public override void Update(GraphicElement el, string label)
		{
            // X1
            //(label == nameof(StartCap)).If(()=> this.ChangePropertyWithUndoRedo<AvailableLineCap>(el, nameof(StartCap), nameof(StartCap)));
            //(label == nameof(StartCap)).If(() => this.ChangePropertyWithUndoRedo<AvailableLineCap>(el, nameof(EndCap), nameof(EndCap)));
            (label == nameof(StartCap)).If(() => ((Connector)el).StartCap = StartCap);
            (label == nameof(EndCap)).If(() => ((Connector)el).EndCap = EndCap);
            (label == nameof(LabelOffset)).If(() => ((Connector)el).LabelOffset = LabelOffset);
            (label == nameof(LabelSize)).If(() => ((Connector)el).LabelSize = LabelSize);
            base.Update(el, label);
		}
	}
}
