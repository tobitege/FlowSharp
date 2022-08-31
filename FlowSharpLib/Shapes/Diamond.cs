﻿/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

using Clifton.Core.ExtensionMethods;

using FlowSharpCodeShapeInterfaces;

namespace FlowSharpLib
{
    [ToolboxOrder(3)]
    public class Diamond : GraphicElement, IIfBox
    {
        public TruePath TruePath { get; set; }

        protected Point[] path;

        public Diamond(Canvas canvas) : base(canvas)
        {
            HasCornerConnections = false;
        }

        public override ElementProperties CreateProperties()
        {
            return new DiamondProperties(this);
        }

        public override void UpdatePath()
        {
            path = new Point[]
            {
                new Point(ZoomRectangle.X, ZoomRectangle.Y + ZoomRectangle.Height/2),
                new Point(ZoomRectangle.X + ZoomRectangle.Width/2, ZoomRectangle.Y),
                new Point(ZoomRectangle.X + ZoomRectangle.Width,   ZoomRectangle.Y + ZoomRectangle.Height/2),
                new Point(ZoomRectangle.X + ZoomRectangle.Width/2, ZoomRectangle.Y + ZoomRectangle.Height),
            };
        }

        public override void Serialize(ElementPropertyBag epb, IEnumerable<GraphicElement> elementsBeingSerialized)
        {
            Json["TruePath"] = TruePath.ToString();
            base.Serialize(epb, elementsBeingSerialized);
        }

        public override void Deserialize(ElementPropertyBag epb)
        {
            base.Deserialize(epb);

            if (Json.TryGetValue("TruePath", out var truePath))
            {
                TruePath = (TruePath)Enum.Parse(typeof(TruePath), truePath);
            }
        }

        public override void Draw(Graphics gr, bool showSelection = true)
        {
            gr.FillPolygon(FillBrush, path);
            gr.DrawPolygon(BorderPen, path);
            base.Draw(gr, showSelection);
        }
    }

    public class DiamondProperties : ElementProperties
    {
        [Category("Logic")]
        public TruePath TruePath { get; set; }

        public DiamondProperties(Diamond el) : base(el)
        {
            TruePath = el.TruePath;
        }

        public override void Update(GraphicElement el, string label)
        {
            (label == nameof(TruePath)).If(() => ((Diamond)el).TruePath = TruePath);
            base.Update(el, label);
        }
    }
}
