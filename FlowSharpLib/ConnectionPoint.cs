/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System.Drawing;

namespace FlowSharpLib
{
    public enum GripType
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        LeftMiddle,
        RightMiddle,
        TopMiddle,
        BottomMiddle,

        // for lines:
        Start,
        End,

        // Other anchor points:
        Center,
    };

    public class ConnectionPoint
    {
        // Setters should be protected, but serializer requires them to be public.
        public GripType Type { get; set; }
        public Point Point { get; set; }

        // Solely for serializer.
        public ConnectionPoint()
        {
        }

        public ConnectionPoint(GripType pos, Point p)
        {
            Type = pos;
            Point = p;
        }

        public static bool operator ==(ConnectionPoint cp1, ConnectionPoint cp2)
        {
            if (ReferenceEquals(cp1, cp2))
            {
                return true;
            }

            if (cp1 is null || cp2 is null)
            {
                return false;
            }

            return cp1.Type == cp2.Type && cp1.Point == cp2.Point;
        }

        public static bool operator !=(ConnectionPoint cp1, ConnectionPoint cp2)
        {
            return !(cp1 == cp2);
        }

        public override bool Equals(object obj)
        {
            return (obj is ConnectionPoint cp && cp == this);
        }

        public override int GetHashCode()
        {
            return Type.GetHashCode() ^ Point.GetHashCode();
        }
    }
}
