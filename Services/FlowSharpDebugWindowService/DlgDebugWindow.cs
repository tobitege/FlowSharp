using System;
using System.Linq;
using System.Windows.Forms;

using Clifton.Core.ServiceManagement;

using FlowSharpLib;
using FlowSharpServiceInterfaces;

namespace FlowSharpDebugWindowService
{
    public partial class DlgDebugWindow : Form
    {
        protected IServiceManager serviceManager;
        protected GraphicElement selectedElement;

        public DlgDebugWindow(IServiceManager serviceManager)
        {
            this.serviceManager = serviceManager;
            InitializeComponent();
            PopulateWithShapes();
            tvShapes.ExpandAll();
            tvShapes.AfterSelect += OnSelect;
            // Clear the selected element when the user focuses on the shape list, so that the user can actually
            // select here, the selected element, and it is visually indicated.
            tvShapes.GotFocus += (sndr, args) => selectedElement = null;
            tvShapes.LostFocus += OnLostFocus;
            ckTraceEnabled.CheckedChanged += OnTraceEnabledCheckedChanged;
        }

        public void Trace(string msg)
        {
            if (!ckTraceEnabled.Checked) return;
            if (msg.StartsWith("Route:") && ckRoutingEvents.Checked)
            {
                tbTrace.AppendText(msg);
            }

            if (msg.StartsWith("Shape:") && ckShapeEvents.Checked)
            {
                tbTrace.AppendText(msg);
            }

            // Always emit messages starting with *** as this is an important notification of a possible error.
            if (msg.StartsWith("***"))
            {
                tbTrace.AppendText(msg);
            }
        }

        public void UpdateUndoStack()
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var undoEvents = canvasController.UndoStack.GetStackInfo();

            tbUndoEvents.Clear();
            //undoEvents.Where(s=>s.EndsWith("F")).ForEach(s => tbUndoEvents.AppendText(s+"\r\n"));
            undoEvents.ForEach(s => tbUndoEvents.AppendText(s + "\r\n"));
        }

        public void UpdateShapeTree()
        {
            System.Diagnostics.Trace.WriteLine("*** UpdateShapeTree!");
            tvShapes.Nodes.Clear();
            PopulateWithShapes();
            tvShapes.ExpandAll();
        }

        public void FindShape(GraphicElement shape)
        {
            selectedElement = shape;
            if (shape == null) return;

            var node = tvShapes.Nodes.Cast<TreeNode>().SingleOrDefault(n => n.Tag == shape);
            if (node == null) return;
            System.Diagnostics.Trace.WriteLine("*** Select Node " + shape.ToString());
            tvShapes.SelectedNode = node;
        }

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            UpdateShapeTree();
        }

        protected void PopulateWithShapes()
        {
            var controller = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            foreach (var el in controller.Elements.OrderBy(el=>controller.Elements.IndexOf(el)))
            {
                var node = CreateTreeNode(el);

                if (el.IsConnector)
                {
                    var c = (Connector)el;
                    ShowConnectedShapes(node, c);
                }

                ShowConnectors(node, el);
                ShowGroupedChildren(node, el);

                tvShapes.Nodes.Add(node);
            }
        }

        protected void ShowConnectors(TreeNode node, GraphicElement el)
        {
            if (el.Connections?.Any() != true) return;
            var connectors = new TreeNode("Connectors");
            node.Nodes.Add(connectors);
            AddConnections(connectors, el);
        }

        protected void ShowConnectedShapes(TreeNode node, Connector c)
        {
            if (c.StartConnectedShape != null)
            {
                node.Nodes.Add(CreateTreeNode(c.StartConnectedShape, "Start: "));
            }

            if (c.EndConnectedShape != null)
            {
                node.Nodes.Add(CreateTreeNode(c.EndConnectedShape, "End: "));
            }
        }

        protected void ShowGroupedChildren(TreeNode node, GraphicElement el)
        {
            if (el.GroupChildren?.Any() != true) return;
            var children = new TreeNode("Children");
            node.Nodes.Add(children);

            foreach (var child in el.GroupChildren)
            {
                var childNode = CreateTreeNode(child);
                children.Nodes.Add(childNode);

                // TODO: Same code as in PopulateWithShapes
                if (child.IsConnector)
                {
                    var c = (Connector)child;
                    ShowConnectedShapes(childNode, c);
                }

                ShowConnectors(childNode, child);
                ShowGroupedChildren(childNode, child);
            }
        }

        protected void AddConnections(TreeNode node, GraphicElement el)
        {
            el.Connections.ForEach(c =>
            {
                node.Nodes.Add(CreateTreeNode(c.ToElement));
            });
        }

        protected TreeNode CreateTreeNode(GraphicElement el, string prefix = "")
        {
            var node = new TreeNode(prefix + el.ToString())
            {
                Tag = el
            };

            return node;
        }

        private void BtnClearTrace_Click(object sender, EventArgs e)
        {
            tbTrace.Text = "";
        }

        private void OnTraceEnabledCheckedChanged(object sender, EventArgs e)
        {
            ckRoutingEvents.Enabled = ckTraceEnabled.Checked;
            ckShapeEvents.Enabled = ckTraceEnabled.Checked;
        }

        private void OnLostFocus(object sender, EventArgs e)
        {
            selectedElement = null;
            var controller = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            UntagTaggedElement(controller);
        }

        private void OnSelect(object sender, TreeViewEventArgs e)
        {
            var elTag = (GraphicElement)e.Node?.Tag;

            if (elTag == null || elTag == selectedElement) return;
            var controller = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            UntagTaggedElement(controller);
            elTag.SetTag();
            elTag.Redraw();
            controller.FocusOn(elTag);
        }

        protected void UntagTaggedElement(BaseController controller)
        {
            var untag = controller.Elements.FirstOrDefault(el => el.Tagged);
            untag?.ClearTag();
            untag?.Redraw();
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            var el = (GraphicElement)tvShapes.SelectedNode?.Tag;
            if (el == null) return;
            var controller = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            controller.DeleteElement(el);
            UpdateShapeTree();
        }
    }
}
