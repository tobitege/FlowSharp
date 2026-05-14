using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using Clifton.Core.ServiceManagement;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpEditService;
using FlowSharpLib;
using FlowSharpServiceInterfaces;

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

        [TestMethod]
        public void EditText_CanStartConnectorLabelEditing()
        {
            Canvas canvas = CreateCanvas();
            DynamicConnectorLR connector = new DynamicConnectorLR(canvas, new Point(10, 10), new Point(90, 40))
            {
                Text = "label"
            };
            canvas.Controller.AddElement(connector);
            canvas.Controller.SelectElement(connector);
            var canvasService = new TestCanvasService(canvas.Controller);
            var editService = new TestFlowSharpEditService();
            var serviceManager = new ServiceManager();
            serviceManager.RegisterSingleton<IFlowSharpCanvasService>(canvasService);
            serviceManager.RegisterSingleton<IFlowSharpEditService>(editService);
            canvasService.Initialize(serviceManager);
            editService.Initialize(serviceManager);

            editService.EditText();

            Assert.IsNotNull(editService.ActiveEditBox);
            Assert.AreEqual("label", editService.ActiveEditBox.Text);
            Assert.AreEqual(connector.GetLabelDisplayRectangle().Location, editService.ActiveEditBox.Location);
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
            public TextBox ActiveEditBox => editBox;

            public void InvokeRestoreConnections(List<ZOrderMap> zorder)
            {
                RestoreConnections(zorder);
            }
        }

        private sealed class TestCanvasService : ServiceBase, IFlowSharpCanvasService
        {
            event System.EventHandler<System.EventArgs> IFlowSharpCanvasService.AddCanvas
            {
                add { }
                remove { }
            }

            event System.EventHandler<FileEventArgs> IFlowSharpCanvasService.LoadLayout
            {
                add { }
                remove { }
            }

            event System.EventHandler<FileEventArgs> IFlowSharpCanvasService.SaveLayout
            {
                add { }
                remove { }
            }

            public BaseController ActiveController { get; }
            public List<BaseController> Controllers => new List<BaseController> { ActiveController };

            public TestCanvasService(BaseController activeController)
            {
                ActiveController = activeController;
            }

            public void CreateCanvas(Control parent)
            {
                throw new System.NotSupportedException();
            }

            public void DeleteCanvas(Control parent)
            {
                throw new System.NotSupportedException();
            }

            public void SetActiveController(Control parent)
            {
                throw new System.NotSupportedException();
            }

            public void RequestNewCanvas()
            {
                throw new System.NotSupportedException();
            }

            public void LoadDiagrams(string filename)
            {
                throw new System.NotSupportedException();
            }

            public void SaveDiagramsAndLayout(string filename, bool selectionOnly = false)
            {
                throw new System.NotSupportedException();
            }

            public void RebaseFilenamesOnNextSave()
            {
            }

            public void ClearControllers()
            {
                throw new System.NotSupportedException();
            }
        }
    }
}
