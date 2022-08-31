/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

using Clifton.Core.ExtensionMethods;

namespace FlowSharpLib
{
    public class ChildPropertyBag
    {
        public Guid ChildId { get; set; }
    }

    public class ConnectionPropertyBag
    {
        public Guid ToElementId { get; set; }
        public ConnectionPoint ToConnectionPoint { get; set; }
        public ConnectionPoint ElementConnectionPoint { get; set; }
    }

    public class ElementPropertyBag
    {
        // For deserialization fixups.
        [XmlIgnore]
        public GraphicElement Element { get; set; }

        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public string ElementName { get; set; }
        [XmlAttribute]
        public Guid Id { get; set; }
        [XmlAttribute]
        public bool Visible { get; set; }
        [XmlAttribute]
        public string Text { get; set; }
        [XmlAttribute]
        public bool IsBookmarked { get; set; }

        public Rectangle DisplayRectangle { get; set; }
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }
        public int HyAdjust { get; set; }
        public int VxAdjust { get; set; }

        public int BorderPenWidth { get; set; }

        [XmlAttribute]
        public bool HasCornerAnchors { get; set; }
        [XmlAttribute]
        public bool HasCenterAnchors { get; set; }
        [XmlAttribute]
        public bool HasLeftRightAnchors { get; set; }
        [XmlAttribute]
        public bool HasTopBottomAnchors { get; set; }
        [XmlAttribute]
        public bool HasCenterAnchor { get; set; }

        [XmlAttribute]
        public bool HasCornerConnections { get; set; }
        [XmlAttribute]
        public bool HasCenterConnections { get; set; }
        [XmlAttribute]
        public bool HasLeftRightConnections { get; set; }
        [XmlAttribute]
        public bool HasTopBottomConnections { get; set; }
        [XmlAttribute]
        public bool HasCenterConnection { get; set; }

        public AvailableLineCap StartCap { get; set; }
        public AvailableLineCap EndCap { get; set; }

        public Guid StartConnectedShapeId { get; set; }
        public Guid EndConnectedShapeId { get; set; }

        public string TextFontFamily { get; set; }
        public float TextFontSize { get; set; }
        public bool TextFontUnderline { get; set; }
        public bool TextFontStrikeout { get; set; }
        public bool TextFontItalic { get; set; }
        public bool TextFontBold { get; set; }

        // TODO: Deprecated with the addition of the JSON string?
        public string ExtraData { get; set; }

        public string Json { get; set; }

        public List<ConnectionPropertyBag> Connections { get; set; }
        public List<ChildPropertyBag> Children { get; set; }

        public ContentAlignment TextAlign { get; set; }
        public bool Multiline { get; set; }

        [XmlIgnore]
        public Color TextColor { get; set; }
        [XmlElement("TextColor")]
        public int XTextColor
        {
            get => TextColor.ToArgb();
            set => TextColor = Color.FromArgb(value);
        }

        [XmlIgnore]
        public Color BorderPenColor { get; set; }
        [XmlElement("BorderPenColor")]
        public int XBorderPenColor
        {
            get => BorderPenColor.ToArgb();
            set => BorderPenColor = Color.FromArgb(value);
        }

        [XmlIgnore]
        public Color FillBrushColor { get; set; }
        [XmlElement("FillBrushColor")]
        public int XFillBrushColor
        {
            get => FillBrushColor.ToArgb();
            set => FillBrushColor = Color.FromArgb(value);
        }

        public ElementPropertyBag()
        {
            Connections = new List<ConnectionPropertyBag>();
            Children = new List<ChildPropertyBag>();
            Visible = true;     // Default, if not defined, for older fsd's that don't have this property.
        }
    }

    public static class Persist
    {
        public static Func<AssemblyName, Assembly> AssemblyResolver { get; set; }
        public static Func<Assembly, string, bool, Type> TypeResolver { get; set; }

        public static string Serialize(IEnumerable<GraphicElement> elements)
        {
            var sps = new List<ElementPropertyBag>();
            elements.ToList().ForEach(el =>
            {
                var epb = new ElementPropertyBag();
                el.Serialize(epb, elements);
                sps.Add(epb);
            });
            var xs = new XmlSerializer(sps.GetType());
            var sb = new StringBuilder();
            TextWriter tw = new StringWriter(sb);
            xs.Serialize(tw, sps);
            return sb.ToString();
        }

        /*
        public static string Serialize(GraphicElement el)
        {
            ElementPropertyBag epb = new ElementPropertyBag();
            el.Serialize(epb, new List<GraphicElement>() { el });
            XmlSerializer xs = new XmlSerializer(typeof(ElementPropertyBag));
            StringBuilder sb = new StringBuilder();
            TextWriter tw = new StringWriter(sb);
            xs.Serialize(tw, epb);
            return sb.ToString();
        }
        */

        /// <summary>
        /// Remap is false when loading from a file, true when copying and pasting.
        /// </summary>
        public static List<GraphicElement> Deserialize(Canvas canvas, string data)
        {
            var oldNewIdMap = new Dictionary<Guid, Guid>();
            var collections = InternalDeserialize(canvas, data, oldNewIdMap);
            FixupConnections(collections, oldNewIdMap);
            FixupChildren(collections, oldNewIdMap);
            FinalFixup(collections, oldNewIdMap);
            return collections.Item1;
        }

        /*
        public static GraphicElement DeserializeElement(Canvas canvas, string data)
        {
            XmlSerializer xs = new XmlSerializer(typeof(ElementPropertyBag));
            TextReader tr = new StringReader(data);
            ElementPropertyBag epb = (ElementPropertyBag)xs.Deserialize(tr);
            Type t = Type.GetType(epb.ElementName);
            GraphicElement el = (GraphicElement)Activator.CreateInstance(t, new object[] { canvas });
            el.Deserialize(epb);        // A specific deserialization does not preserve connections.
            el.Id = Guid.NewGuid();     // We get a new GUID when deserializing a specific element.

            return el;
        }
        */

        private static Tuple<List<GraphicElement>, List<ElementPropertyBag>> InternalDeserialize(Canvas canvas, string data, Dictionary<Guid, Guid> oldNewIdMap)
        {
            var elements = new List<GraphicElement>();
            var xs = new XmlSerializer(typeof(List<ElementPropertyBag>));
            TextReader tr = new StringReader(data);
            List<ElementPropertyBag> sps = (List<ElementPropertyBag>)xs.Deserialize(tr);

            foreach (var epb in sps)
            {
                var t = Type.GetType(epb.ElementName, AssemblyResolver, TypeResolver);
                var el = (GraphicElement)Activator.CreateInstance(t, new object[] { canvas });
                el.Deserialize(epb);
                var elGuid = Guid.NewGuid();
                oldNewIdMap[el.Id] = elGuid;
                el.Id = elGuid;
                elements.Add(el);
                epb.Element = el;
            }

            return new Tuple<List<GraphicElement>, List<ElementPropertyBag>>(elements, sps);
        }

        private static void FixupConnections(Tuple<List<GraphicElement>, List<ElementPropertyBag>> collections, Dictionary<Guid, Guid> oldNewGuidMap)
        {
            foreach (var epb in collections.Item2)
            {
                epb.Connections.Where(c => c.ToElementId != Guid.Empty).ForEach(c =>
                {
                    var conn = new Connection();
                    conn.Deserialize(collections.Item1, c, oldNewGuidMap);
                    epb.Element.Connections.Add(conn);
                });
            }
        }

        private static void FixupChildren(Tuple<List<GraphicElement>, List<ElementPropertyBag>> collections, Dictionary<Guid, Guid> oldNewGuidMap)
        {
            var elements = collections.Item1;

            foreach (var epb in collections.Item2)
            {
                var el = elements.Single(e => e.Id == oldNewGuidMap[epb.Id]);
                foreach (var cpb in epb.Children)
                {
                    var child = elements.Single(e => e.Id == oldNewGuidMap[cpb.ChildId]);
                    el.GroupChildren.Add(child);
                    child.Parent = el;
                }
            }
        }

        private static void FinalFixup(Tuple<List<GraphicElement>, List<ElementPropertyBag>> collections, Dictionary<Guid, Guid> oldNewGuidMap)
        {
            collections.Item2.ForEach(epb => epb.Element.FinalFixup(collections.Item1, epb, oldNewGuidMap));
        }
    }
}
