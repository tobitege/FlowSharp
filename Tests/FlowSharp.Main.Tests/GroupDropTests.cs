using System.Drawing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpLib;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class GroupDropTests
    {
        [TestMethod]
        public void GroupBoxMove_MovesExternalConnectorOncePerEndpoint()
        {
            Canvas canvas = CreateCanvas();
            var controller = new CanvasController(canvas);
            GroupBox group = new GroupBox(canvas)
            {
                DisplayRectangle = new Rectangle(20, 20, 120, 100)
            };
            Box child = new Box(canvas)
            {
                DisplayRectangle = new Rectangle(40, 40, 40, 30)
            };
            child.Parent = group;
            group.GroupChildren.Add(child);

            DiagonalConnector connector = new DiagonalConnector(canvas, new Point(child.DisplayRectangle.Right, child.DisplayRectangle.Top + child.DisplayRectangle.Height / 2), new Point(200, 60));
            Connection duplicateA = new Connection
            {
                ToElement = connector,
                ToConnectionPoint = new ConnectionPoint(GripType.Start, connector.StartPoint),
                ElementConnectionPoint = new ConnectionPoint(GripType.RightMiddle, new Point(child.DisplayRectangle.Right, child.DisplayRectangle.Top + child.DisplayRectangle.Height / 2))
            };
            Connection duplicateB = new Connection
            {
                ToElement = connector,
                ToConnectionPoint = new ConnectionPoint(GripType.Start, connector.StartPoint),
                ElementConnectionPoint = new ConnectionPoint(GripType.RightMiddle, new Point(child.DisplayRectangle.Right, child.DisplayRectangle.Top + child.DisplayRectangle.Height / 2))
            };

            child.Connections.Add(duplicateA);
            child.Connections.Add(duplicateB);

            Point originalStart = connector.StartPoint;

            group.Move(new Point(10, 5));

            Assert.AreEqual(originalStart.X + 10, connector.StartPoint.X);
            Assert.AreEqual(originalStart.Y + 5, connector.StartPoint.Y);
        }

        [TestMethod]
        public void AddAndRemoveShapeToGroup_UpdatesParentRelationship()
        {
            Canvas canvas = CreateCanvas();
            var controller = new CanvasController(canvas);
            GroupBox group = new GroupBox(canvas)
            {
                DisplayRectangle = new Rectangle(20, 20, 140, 120)
            };
            Box shape = new Box(canvas)
            {
                DisplayRectangle = new Rectangle(40, 40, 40, 30)
            };

            controller.AddElement(group);
            controller.AddElement(shape);

            GroupBox targetGroup = controller.GetContainingGroupBox(shape);
            controller.AddShapeToGroup(targetGroup, shape);

            Assert.AreSame(group, targetGroup);
            Assert.AreSame(group, shape.Parent);
            CollectionAssert.Contains(group.GroupChildren, shape);

            controller.RemoveShapeFromGroup(group, shape);

            Assert.IsNull(shape.Parent);
            CollectionAssert.DoesNotContain(group.GroupChildren, shape);
        }

        private static Canvas CreateCanvas()
        {
            Canvas canvas = new Canvas();
            canvas.CreateBitmap(240, 180);
            return canvas;
        }
    }
}
