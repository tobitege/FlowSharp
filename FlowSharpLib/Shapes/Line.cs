/* 
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System.Drawing.Drawing2D;

namespace FlowSharpLib
{
	public abstract class Line : Connector
	{
        public Line(Canvas canvas) : base(canvas)
		{
        }

		public override ElementProperties CreateProperties()
		{
			return new LineProperties(this);
		}

        public override void UpdateProperties()
        {
            ApplyStartCap(StartCap);
            ApplyEndCap(EndCap);

            base.UpdateProperties();
        }
    }
}
