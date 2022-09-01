﻿/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

using Newtonsoft.Json.Linq;

using FlowSharpLib;

// localhost:8001/flowsharp?cmd=CmdUpdateProperty&Name=cbItems&PropertyName=JsonItems&Value=[{'id':'0','name':'foo'},{'id':'1','name':'fiz bin'}]

namespace FlowSharpWindowsControlShapes
{
    public class ComboboxItem
    {
        public object Id { get; set; }
        public object Display { get; set; }
    }

    [ExcludeFromToolbox]
    public class ComboboxShape : ControlShape
    {
        protected string jsonItems;

        public string IdFieldName { get; set; }
        public string DisplayFieldName { get; set; }

        public string JsonItems
        {
            get => jsonItems;
            set
            {
                jsonItems = value;
                UpdateList();
            }
        }

        public ComboboxShape(Canvas canvas) : base(canvas)
        {
            var cb = new ComboBox();
            control = cb;
            canvas.Controls.Add(control);
            cb.ValueMember = "Id";
            cb.DisplayMember = "Display";
            cb.SelectedIndexChanged += OnSelectedIndexChanged;
        }

        private void OnSelectedIndexChanged(object sender, System.EventArgs e)
        {
            Send("ItemSelected");
        }

        protected override string AppendData(string data)
        {
            var cb = (ComboBox)control;
            var id = ((ComboboxItem)cb.SelectedItem).Id.ToString();
            data = base.AppendData(data);
            data += "&Id=" + id;
            return data;
        }

        public override ElementProperties CreateProperties()
        {
            return new ComboboxShapeProperties(this);
        }

        public override void Serialize(ElementPropertyBag epb, IEnumerable<GraphicElement> elementsBeingSerialized)
        {
            Json["IdFieldName"] = IdFieldName;
            Json["DisplayFieldName"] = DisplayFieldName;
            base.Serialize(epb, elementsBeingSerialized);
        }

        public override void Deserialize(ElementPropertyBag epb)
        {
            base.Deserialize(epb);
            Json.TryGetValue("IdFieldName", out var idFieldName);
            Json.TryGetValue("DisplayFieldName", out var valueFieldName);

            DisplayFieldName = valueFieldName;
            IdFieldName = idFieldName;
        }

        public override void Draw(Graphics gr, bool showSelection = true)
        {
            base.Draw(gr, showSelection);
            var r = ZoomRectangle.Grow(-4);

            if (control.Location != r.Location)
            {
                control.Location = r.Location;
            }
            if (control.Size != r.Size)
            {
                // Use the control's height so we don't get continuous redraws.
                control.Size = new Size(r.Width, control.Height);
            }
            if (control.Enabled != Enabled)
            {
                control.Enabled = Enabled;
            }

            if (control.Visible != Visible)
            {
                control.Visible = Visible;
            }
        }

        /// <summary>
        /// Map "Id" and "Display" in ComboboxItem to the ID and display field names in the JSON.
        /// </summary>
        private void UpdateList()
        {
            var cb = (ComboBox)control;
            cb.Items.Clear();

            dynamic items = JArray.Parse(JsonItems);
            var cbItems = new List<ComboboxItem>();

            foreach (var item in items)
            {
                var cbItem = new ComboboxItem
                {
                    Id = item[IdFieldName],
                    Display = item[DisplayFieldName].ToString()
                };
                cbItems.Add(cbItem);
            }
            cb.Items.AddRange(cbItems.ToArray());
            if (cbItems.Count > 0)
            {
                cb.SelectedIndex = 0;
            }
        }
    }

    [ToolboxShape]
    public class ToolboxComboboxShape : GraphicElement
    {
        public const string TOOLBOX_TEXT = "cmbbox";

        protected readonly Brush brush = new SolidBrush(Color.Black);

        public ToolboxComboboxShape(Canvas canv) : base(canv)
        {
            TextFont.Dispose();
            TextFont = new Font(FontFamily.GenericSansSerif, 8);
        }

        public override GraphicElement CloneDefault(Canvas canv)
        {
            return CloneDefault(canv, Point.Empty);
        }

        public override GraphicElement CloneDefault(Canvas canv, Point offset)
        {
            var shape = new ComboboxShape(canv);
            shape.DisplayRectangle = shape.DefaultRectangle().Move(offset);
            shape.UpdateProperties();
            shape.UpdatePath();
            return shape;
        }

        public override void Draw(Graphics gr, bool showSelection = true)
        {
            var size = gr.MeasureString(TOOLBOX_TEXT, TextFont);
            var textpos = DisplayRectangle.Center().Move((int)(-size.Width / 2), (int)(-size.Height / 2));
            gr.DrawString(TOOLBOX_TEXT, TextFont, brush, textpos);
            base.Draw(gr, showSelection);
        }
    }
}
