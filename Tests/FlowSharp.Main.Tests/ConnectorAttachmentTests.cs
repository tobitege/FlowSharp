using System.Collections.Generic;
using System.Drawing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpEditService;
using FlowSharpLib;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class ConnectorAttachmentTests
    {
        [TestMethod]
        public void SnapActionAttach_DoesNotDuplicateSameConnectorEndpoint()
        {
            Canvas canvas = CreateCanvas();
            Box shape = new Box(canvas)
            {
                DisplayRectangle = new Rectangle(20, 20, 60, 40)
            };
            DiagonalConnector connector = new DiagonalConnector(canvas, new Point(10, 10), new Point(80, 80));
            ConnectionPoint lineConnectionPoint = new ConnectionPoint(GripType.Start, connector.StartPoint);
            ConnectionPoint shapeConnectionPoint = new ConnectionPoint(GripType.RightMiddle, new Point(shape.DisplayRectangle.Right, shape.DisplayRectangle.Top + shape.DisplayRectangle.Height / 2));
            var action = new SnapAction(SnapAction.Action.Attach, connector, GripType.Start, shape, lineConnectionPoint, shapeConnectionPoint, Point.Empty);

            action.Attach();
            action.Attach();

            Assert.AreEqual(1, shape.Connections.Count);
        }

        [TestMethod]
        public void RestoreConnections_DoesNotDuplicateConnectionWhenShapeAndConnectorAreRestoredTogether()
        {
            Canvas canvas = CreateCanvas();
            Box shape = new Box(canvas)
            {
                DisplayRectangle = new Rectangle(20, 20, 60, 40)
            };
            DiagonalConnector connector = new DiagonalConnector(canvas, new Point(10, 10), new Point(80, 80));
            var connection = new Connection
            {
                ToElement = connector,
                ToConnectionPoint = new ConnectionPoint(GripType.Start, connector.StartPoint),
                ElementConnectionPoint = new ConnectionPoint(GripType.RightMiddle, new Point(shape.DisplayRectangle.Right, shape.DisplayRectangle.Top + shape.DisplayRectangle.Height / 2))
            };

            shape.AddConnection(connection);
            connector.StartConnectedShape = shape;

            var zorder = new List<ZOrderMap>
            {
                new ZOrderMap
                {
                    Element = shape,
                    Connections = new List<Connection> { connection }
                },
                new ZOrderMap
                {
                    Element = connector,
                    Connections = new List<Connection>(),
                    StartConnectedShape = shape,
                    StartConnection = connection
                }
            };

            var service = new TestFlowSharpEditService();
            service.InvokeRestoreConnections(zorder);

            Assert.AreEqual(1, shape.Connections.Count);
            Assert.AreSame(shape, connector.StartConnectedShape);
        }

        private static Canvas CreateCanvas()
        {
            Canvas canvas = new Canvas();
            canvas.CreateBitmap(120, 120);
            _ = new CanvasController(canvas);
            return canvas;
        }

        private sealed class TestFlowSharpEditService : FlowSharpEditService.FlowSharpEditService
        {
            public void InvokeRestoreConnections(List<ZOrderMap> zorder)
            {
                RestoreConnections(zorder);
            }
        }
    }
}
