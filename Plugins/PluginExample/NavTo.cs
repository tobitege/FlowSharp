/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Clifton.Core.ExtensionMethods;

using FlowSharpLib;

namespace PluginExample
{
    public class NavTo : Box
    {
        public string NavigateTo { get; set; }

        protected Rectangle target;

        public NavTo(Canvas canvas) : base(canvas)
        {
            TextAlign = ContentAlignment.TopCenter;
        }

        public override void ElementSelected()
        {
            base.ElementSelected();
            var p = Cursor.Position;
            p = canvas.FindForm().PointToClient(p);

            if (!target.Contains(p)) return;
            var navtoname = string.IsNullOrEmpty(NavigateTo) ? Text : NavigateTo;
            var navto = canvas.Controller.Elements.Where(el => el.Name == navtoname);

            if (!navto.Any()) return;
            canvas.FindForm().Cursor = Cursors.WaitCursor;
            canvas.Controller.FocusOn(navto.First());
            canvas.FindForm().Cursor = Cursors.Arrow;
        }

        public override ElementProperties CreateProperties()
        {
            return new NavToProperties(this);
        }

        public override void Serialize(ElementPropertyBag epb, IEnumerable<GraphicElement> elementsBeingSerialized)
        {
            Json["NavigateTo"] = NavigateTo;
            base.Serialize(epb, elementsBeingSerialized);
        }

        public override void Deserialize(ElementPropertyBag epb)
        {
            base.Deserialize(epb);
            Json.TryGetValue("NavigateTo", out var navigateTo);
            NavigateTo = navigateTo;
        }

        public override void Draw(Graphics gr, bool showSelection = true)
        {
            base.Draw(gr, showSelection);
            var min = ZoomRectangle.Width.Min(ZoomRectangle.Height) / 2;
            var topleft = ZoomRectangle.Center();
            topleft.X -= min / 2;
            topleft.Y -= 3;
            target = new Rectangle(topleft, new Size(min, min));
            var innerTopLeft = target.Center();
            innerTopLeft.Offset(-min / 4, -min / 4);
            var targetInner = new Rectangle(innerTopLeft, new Size(min/2, min/2));
            var fillBrush = new SolidBrush(Color.White);
            var middleTarget = new SolidBrush(Color.Red);
            var borderPen = new Pen(Color.Red);
            gr.FillEllipse(fillBrush, target);
            gr.FillEllipse(middleTarget, targetInner);
            gr.DrawEllipse(borderPen, target);
            fillBrush.Dispose();
            borderPen.Dispose();
            middleTarget.Dispose();
        }
    }
}
