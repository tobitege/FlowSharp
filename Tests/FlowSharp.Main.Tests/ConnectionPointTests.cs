using System.Drawing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpLib;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class ConnectionPointTests
    {
        [TestMethod]
        public void EqualityOperators_HandleNullsWithoutRecursion()
        {
            ConnectionPoint point = new ConnectionPoint(GripType.Start, new Point(10, 20));
            ConnectionPoint nullPoint = null;

            Assert.IsFalse(point == nullPoint);
            Assert.IsFalse(nullPoint == point);
            Assert.IsTrue(nullPoint == null);
            Assert.IsTrue(point != nullPoint);
            Assert.IsTrue(nullPoint != point);
            Assert.IsFalse(nullPoint != null);
        }

        [TestMethod]
        public void EqualityOperators_CompareTypeAndPoint()
        {
            var first = new ConnectionPoint(GripType.Start, new Point(10, 20));
            var same = new ConnectionPoint(GripType.Start, new Point(10, 20));
            var differentType = new ConnectionPoint(GripType.End, new Point(10, 20));
            var differentPoint = new ConnectionPoint(GripType.Start, new Point(20, 10));

            Assert.IsTrue(first == same);
            Assert.IsFalse(first != same);
            Assert.IsFalse(first == differentType);
            Assert.IsFalse(first == differentPoint);
        }
    }
}
