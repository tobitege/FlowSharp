﻿/* 
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

using Clifton.Core.ExtensionMethods;

using FlowSharpLib;
using FlowSharpCodeShapeInterfaces;

namespace FlowSharpCodeShapes
{
    [ExcludeFromToolbox]
    public class AssemblyReferenceBox : Box, IAssemblyReferenceBox
    {
        public string Filename { get; set; }

        public AssemblyReferenceBox(Canvas canvas) : base(canvas)
        {
            Text = "AssyRef";
            TextFont.Dispose();
            TextFont = new Font(FontFamily.GenericSansSerif, 6);
            TextAlign = ContentAlignment.TopCenter;
            Filename = "";
        }

        public override ElementProperties CreateProperties()
        {
            return new AssemblyReferenceBoxProperties(this);
        }

        public override void Serialize(ElementPropertyBag epb, IEnumerable<GraphicElement> elementsBeingSerialized)
        {
            Json["AssyRef"] = Filename;
            base.Serialize(epb, elementsBeingSerialized);
        }

        public override void Deserialize(ElementPropertyBag epb)
        {
            base.Deserialize(epb);
            Filename = Json["AssyRef"];
        }

        public override void DrawText(Graphics gr)
        {
            base.DrawText(gr);
            Font fnFont = new Font(FontFamily.GenericSansSerif, 10);
            DrawText(gr, Filename, fnFont, TextColor, ContentAlignment.BottomCenter);
            fnFont.Dispose();
        }
    }

    [ToolboxShape]
    [ToolboxOrder(8)]
    public class ToolboxAssemblyReferenceBox : Box
    {
        public override Rectangle ToolboxDisplayRectangle => new Rectangle(0, 0, 45, 25);

        public ToolboxAssemblyReferenceBox(Canvas canvas) : base(canvas)
        {
            Text = "AssyRef";
            TextFont.Dispose();
            TextFont = new Font(FontFamily.GenericSansSerif, 6);
            TextAlign = ContentAlignment.MiddleCenter;
            HasCornerConnections = false;
        }

        public override GraphicElement CloneDefault(Canvas canvas)
        {
            return CloneDefault(canvas, Point.Empty);
        }

        public override GraphicElement CloneDefault(Canvas canvas, Point offset)
        {
            AssemblyReferenceBox shape = new AssemblyReferenceBox(canvas);
            shape.DisplayRectangle = shape.DefaultRectangle().Move(offset);
            shape.UpdateProperties();
            shape.UpdatePath();

            return shape;
        }
    }

    public class AssemblyReferenceBoxProperties : ElementProperties
    {
        [Category("Assembly")]
        public string Filename { get; set; }

        public AssemblyReferenceBoxProperties(AssemblyReferenceBox el) : base(el)
        {
            Filename = el.Filename;
        }

        public override void Update(GraphicElement el, string label)
        {
            AssemblyReferenceBox box = (AssemblyReferenceBox)el;

            (label == "Filename").If(() =>
            {
                box.Filename = Filename;
            });

            base.Update(el, label);
        }
    }
}
