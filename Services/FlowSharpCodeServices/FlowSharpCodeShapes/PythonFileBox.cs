/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;

using Clifton.Core.ExtensionMethods;

using FlowSharpLib;
using FlowSharpCodeShapeInterfaces;

namespace FlowSharpCodeShapes
{
    public class PythonFileBox : Box, IPythonClass
    {
        public string Filename { get; set; }
        public bool GenerateClass { get; set; }

        public PythonFileBox(Canvas canvas) : base(canvas)
        {
            Text = ".py";
            TextFont.Dispose();
            TextFont = new Font(FontFamily.GenericSansSerif, 6);
            TextAlign = ContentAlignment.TopLeft;
            GenerateClass = true;
        }

        public void UpdateCodeBehind()
        {
            var data = "";
            if (File.Exists(Filename))
            {
                data = File.ReadAllText(Filename);
            }
            Json["python"] = data;
        }

        public override GraphicElement CloneDefault(Canvas canv)
        {
            var el = base.CloneDefault(canv);
            el.TextFont.Dispose();
            el.TextFont = new Font(FontFamily.GenericSansSerif, 10);
            el.TextAlign = ContentAlignment.TopLeft;
            return el;
        }

        public override GraphicElement CloneDefault(Canvas canv, Point offset)
        {
            GraphicElement el = base.CloneDefault(canv, offset);
            el.TextFont.Dispose();
            el.TextFont = new Font(FontFamily.GenericSansSerif, 10);
            el.TextAlign = ContentAlignment.TopLeft;
            return el;
        }

        public override ElementProperties CreateProperties()
        {
            return new PythonFileBoxProperties(this);
        }

        public override void Serialize(ElementPropertyBag epb, IEnumerable<GraphicElement> elementsBeingSerialized)
        {
            // TODO: Use JSON dictionary instead.
            epb.ExtraData = Filename;

            // Also update the backing file.
            if (Json.ContainsKey("python"))
            {
                File.WriteAllText(Filename, Json["python"]);
            }

            Json["GenerateClass"] = GenerateClass.ToString();
            base.Serialize(epb, elementsBeingSerialized);
        }

        public override void Deserialize(ElementPropertyBag epb)
        {
            base.Deserialize(epb);
            // TODO: Use JSON dictionary instead.
            Filename = epb.ExtraData;

            if (Json.TryGetValue("GenerateClass", out var strGenerateClass))
            {
                GenerateClass = Convert.ToBoolean(strGenerateClass);
            }
        }
    }

    public class PythonFileBoxProperties : ShapeProperties
    {
        [Category("Class")]
        public string Filename { get; set; }
        [Category("Class")]
        public bool GenerateClass { get; set; }

        public PythonFileBoxProperties(PythonFileBox el) : base(el)
        {
            Filename = el.Filename;
            GenerateClass = el.GenerateClass;
        }

        public override void Update(GraphicElement el, string label)
        {
            var box = (PythonFileBox)el;

            (label == nameof(Filename)).If(() =>
            {
                box.Filename = Filename;
                box.UpdateCodeBehind();
                box.Text = string.IsNullOrEmpty(Filename) ? "?.py" : (Path.GetFileName(Filename));
            });

            (label == nameof(GenerateClass)).If(() => box.GenerateClass = GenerateClass);

            base.Update(el, label);
        }
    }
}
