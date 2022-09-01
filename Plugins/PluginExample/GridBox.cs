/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

// Inspired by a plugin that Lucas Martins da Silva created.

using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

using Clifton.Core.ExtensionMethods;

namespace FlowSharpLib.Shapes
{
    public class GridBoxProperties : ElementProperties
    {
        [Category("Dimensions")]
        public int Columns { get; set; }
        [Category("Dimensions")]
        public int Rows { get; set; }

        public GridBoxProperties(GridBox el) : base(el)
        {
            Columns = el.Columns;
            Rows = el.Rows;
        }

        public override void Update(GraphicElement el, string label)
        {
            (label == nameof(Columns)).If(() => ((GridBox)el).Columns = Columns);
            (label == nameof(Rows)).If(() => ((GridBox)el).Rows = Rows);
            base.Update(el, label);
        }
    }

    public struct Cell      // Struct so it can be used as a lookup key in a dictionary.
    {
        public int Column;
        public int Row;

        public override string ToString()
        {
            return Column.ToString() + "," + Row.ToString();
        }
    }

    public class GridBox : GraphicElement
    {
        public int Columns { get; set; }
        public int Rows { get; set; }

        protected Dictionary<Cell, string> cellText;
        protected int editCol;
        protected int editRow;

        public GridBox(Canvas canvas) : base(canvas)
        {
            Columns = 4;
            Rows = 4;
            cellText = new Dictionary<Cell, string>();
        }

        public override ElementProperties CreateProperties()
        {
            return new GridBoxProperties(this);
        }

        public override void Serialize(ElementPropertyBag epb, IEnumerable<GraphicElement> elementsBeingSerialized)
        {
            Json["columns"] = Columns.ToString();
            Json["rows"] = Rows.ToString();
            Json["textFields"] = cellText.Count.ToString();
            var n = 0;

            foreach (var kvp in cellText)
            {
                Json["celltext" + n] = kvp.Key.ToString() + "," + kvp.Value;
                ++n;
            }
            base.Serialize(epb, elementsBeingSerialized);
        }

        public override void Deserialize(ElementPropertyBag epb)
        {
            base.Deserialize(epb);
            Columns = Json["columns"].To_i();
            Rows = Json["rows"].To_i();
            var cellTextCount = Json["textFields"].To_i();

            for (var i = 0; i < cellTextCount; i++)
            {
                var cellInfo = Json["celltext" + i];
                var cellData = cellInfo.Split(',');
                cellText[new Cell() { Column = cellData[0].To_i(), Row = cellData[1].To_i() }] = cellData[2];
            }
        }

        public override TextBox CreateTextBox(Point mousePosition)
        {
            TextBox tb;
            // Get cell where mouse cursor is currently over.
            var localMousePos = Canvas.PointToClient(mousePosition);
            editCol = -1;
            editRow = -1;
            var cellWidth = DisplayRectangle.Width / Columns;
            var cellHeight = DisplayRectangle.Height / Rows;

            if (DisplayRectangle.Contains(localMousePos))
            {
                editCol = (localMousePos.X - DisplayRectangle.Left) / cellWidth;
                editRow = (localMousePos.Y - DisplayRectangle.Top) / cellHeight;
                tb = new TextBox
                {
                    Location = DisplayRectangle.TopLeftCorner().Move(editCol * cellWidth, editRow * cellHeight + cellHeight / 2 - 10),
                    Size = new Size(cellWidth, 20)
                };
                cellText.TryGetValue(new Cell() { Column = editCol, Row = editRow }, out var text);
                tb.Text = text;
            }
            else
            {
                tb = base.CreateTextBox(mousePosition);
            }

            return tb;
        }

        public override void EndEdit(string newVal, string oldVal)
        {
            var editColClosure = editCol;
            var editRowClosure = editRow;
            var cell = new Cell() { Column = editColClosure, Row = editRowClosure };
            cellText.TryGetValue(cell, out var oldValClosure);

            canvas.Controller.UndoStack.UndoRedo("Inline edit",
                () =>
                {
                    canvas.Controller.Redraw(this, (el) => cellText[cell] = newVal);
                    canvas.Controller.ElementSelected.Fire(canvas.Controller, new ElementEventArgs() { Element = this });
                },
                () =>
                {
                    canvas.Controller.Redraw(this, (el) => cellText[cell] = oldValClosure);
                    canvas.Controller.ElementSelected.Fire(canvas.Controller, new ElementEventArgs() { Element = this });
                });
        }

        public override void Draw(Graphics gr, bool showSelection = true)
        {
            var r = DisplayRectangle;
            var cellWidth = DisplayRectangle.Width / Columns;
            var cellHeight = DisplayRectangle.Height / Rows;
            var rects = new RectangleF[Rows * Columns];
            var n = 0;

            for (var x = 0; x < Columns; x++)
            {
                for (var y = 0; y < Rows; y++)
                {
                    rects[n++] = new RectangleF(r.Left + cellWidth * x, r.Top + cellHeight * y, cellWidth, cellHeight);
                }
            }

            gr.FillRectangle(FillBrush, DisplayRectangle);
            gr.DrawRectangles(BorderPen, rects);
            var brush = new SolidBrush(TextColor);

            for (var x = 0; x < Columns; x++)
            {
                for (var y = 0; y < Rows; y++)
                {
                    if (cellText.TryGetValue(new Cell() { Column = x, Row = y }, out var text))
                    {
                        var size = gr.MeasureString(text, TextFont);
                        var rectCell = new Rectangle(r.Left + cellWidth * x, r.Top + cellHeight * y, cellWidth, cellHeight);
                        var textpos = rectCell.Center().Move((int)(-size.Width / 2), (int)(-size.Height / 2));
                        gr.DrawString(text, TextFont, brush, textpos);
                    }
                }
            }
            brush.Dispose();
            base.Draw(gr, showSelection);
        }
    }
}
