/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Clifton.Core.ExtensionMethods;
using Clifton.Core.ServiceManagement;

using FlowSharpLib;
using FlowSharpServiceInterfaces;

namespace FlowSharpToolboxService
{
    public class ToolboxController : BaseController
    {
        public const int MIN_DRAG = 3;

        protected int xDisplacement = 0;
        protected bool mouseDown = false;
        protected Point mouseDownPosition;
        protected Point currentDragPosition;
        protected bool setup;
        protected bool dragging;
        protected GraphicElement draggedCanvasElement;
        protected IServiceManager serviceManager;

        public ToolboxController(IServiceManager serviceManager, Canvas canvas) : base(canvas)
        {
            this.serviceManager = serviceManager;
            canvas.PaintComplete = ToolboxCanvasPaintComplete;
            canvas.MouseClick += OnMouseClick;
            canvas.MouseDown += OnMouseDown;
            canvas.MouseUp += OnMouseUp;
            canvas.MouseMove += OnMouseMove;
        }

        public void ResetDisplacement()
        {
            xDisplacement = 0;
        }

        public void OnMouseClick(object sender, MouseEventArgs args)
        {
        }

        public void OnMouseDown(object sender, MouseEventArgs args)
        {
            if (args.Button != MouseButtons.Left) return;
            var selectedElement = GetSelectedElement(args.Location);
            mouseDown = true;
            mouseDownPosition = args.Location;
            SelectElement(selectedElement);
        }

        public void OnMouseUp(object sender, MouseEventArgs args)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;

            if (args.Button == MouseButtons.Left && !dragging)
            {
                if (selectedElements.Any())
                {
                    CreateShape(null);
                    xDisplacement += 80;
                }
            }
            else if (args.Button == MouseButtons.Left && dragging)
            {
                if (draggedCanvasElement == null)
                {
                    dragging = false;
                    mouseDown = false;
                    DeselectCurrentSelectedElement();
                    selectedElements.Clear();
                    canvas.Cursor = Cursors.Arrow;
                    return;
                }

                // TODO: Similar to MouseController EndShapeDrag/EndAnchorDrag
                canvasController.SnapController.DoUndoSnapActions(canvasController.UndoStack);
                GraphicElement droppedElement = draggedCanvasElement;
                FlowSharpLib.GroupBox targetGroup = droppedElement == null || droppedElement.IsConnector ? null : canvasController.GetContainingGroupBox(droppedElement);

                if (targetGroup != null)
                {
                    canvasController.AddShapeToGroup(targetGroup, droppedElement);
                    serviceManager.Get<IFlowSharpDebugWindowService>().UpdateDebugWindow();
                }

                if (canvasController.SnapController.RunningDelta != Point.Empty || targetGroup != null)
                {
                    var delta = canvasController.SnapController.RunningDelta;     // for closure

                    canvasController.UndoStack.UndoRedo("ShapeMove",
                        () => { },      // Our "do" action is actually nothing, since all the "doing" has been done.
                        () =>           // Undo
                        {
                            if (targetGroup != null)
                            {
                                canvasController.RemoveShapeFromGroup(targetGroup, droppedElement);
                                serviceManager.Get<IFlowSharpDebugWindowService>().UpdateDebugWindow();
                            }

                            if (delta != Point.Empty)
                            {
                                canvasController.DragSelectedElements(delta.ReverseDirection());
                            }
                        },
                        true,           // We finish the move.
                        () =>           // Redo
                        {
                            if (delta != Point.Empty)
                            {
                                canvasController.DragSelectedElements(delta);
                            }

                            if (targetGroup != null)
                            {
                                canvasController.AddShapeToGroup(targetGroup, droppedElement);
                                serviceManager.Get<IFlowSharpDebugWindowService>().UpdateDebugWindow();
                            }
                        });
                }
            }

            dragging = false;
            mouseDown = false;
            draggedCanvasElement = null;
            canvasController.SnapController.HideConnectionPoints();
            DeselectCurrentSelectedElement();
            selectedElements.Clear();
            canvas.Cursor = Cursors.Arrow;
        }

        public void OnMouseMove(object sender, MouseEventArgs args)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;

            if (selectedElements.Count > 0 && mouseDown && selectedElements[0] != null && !dragging)
            {
                var delta = args.Location.Delta(mouseDownPosition);
                if ((Math.Abs(delta.X) > MIN_DRAG) || (Math.Abs(delta.Y) > MIN_DRAG))
                {
                    dragging = true;
                    setup = true;
                    ResetDisplacement();
                    canvas.Cursor = Cursors.SizeAll;
                }
            }
            else if (mouseDown && selectedElements.Any() && dragging)
            {
                // First time event is because we've changed the mouse position.  Reset the current drag position so
                // we get the current mouse position, then clear the flag so drag operations continue to move the shape
                // after our mouse coordinate management is set up correctly.
                if (setup)
                {
                    currentDragPosition = GetCanvasPointerLocation(canvasController);
                    if (!IsPointInsideCanvas(canvasController, currentDragPosition))
                    {
                        return;
                    }

                    draggedCanvasElement = CreateShape(currentDragPosition);
                    setup = false;
                    canvasController.SnapController.Reset();
                }
                else
                {
                    // Toolbox controller still has control, so simulate dragging on the canvas.
                    Point pointer = GetCanvasPointerLocation(canvasController);
                    var delta = pointer.Delta(currentDragPosition);
                    currentDragPosition = pointer;

                    if (delta == Point.Empty) return;
                    // TODO: Duplicate code in FlowSharpUI.DoMove and MouseController
                    if (selectedElements[0].IsConnector)
                    {
                        // Check both ends of any connector being moved.
                        if (!canvasController.SnapController.SnapCheck(GripType.Start, delta, (snapDelta) => canvasController.DragSelectedElements(snapDelta)))
                        {
                            if (!canvasController.SnapController.SnapCheck(GripType.End, delta, (snapDelta) => canvasController.DragSelectedElements(snapDelta)))
                            {
                                canvasController.DragSelectedElements(delta);
                                canvasController.SnapController.UpdateRunningDelta(delta);
                            }
                        }
                    }
                    else
                    {
                        canvasController.DragSelectedElements(delta);
                        canvasController.SnapController.UpdateRunningDelta(delta);
                    }
                }
            }
        }

        protected GraphicElement CreateShape(Point? canvasClientPoint)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var where = xDisplacement;

            // For undo, we need to preserve currently selected shapes.
            var currentSelectedShapes = canvasController.SelectedElements.ToList();
            var selectedElement = selectedElements[0];
            var el = canvasClientPoint.HasValue
                ? selectedElement.CloneDefault(canvasController.Canvas)
                : selectedElement.CloneDefault(canvasController.Canvas, new Point(where, 0));

            if (canvasClientPoint.HasValue)
            {
                Point worldPoint = canvasController.ClientToWorld(canvasClientPoint.Value);
                el.DisplayRectangle = new Rectangle(
                    worldPoint.X - el.DisplayRectangle.Width / 2,
                    worldPoint.Y - el.DisplayRectangle.Height / 2,
                    el.DisplayRectangle.Width,
                    el.DisplayRectangle.Height);
            }

            canvasController.UndoStack.UndoRedo("Create " + el.ToString(),
                () =>
                {
                    // ElementCache.Instance.Remove(el);
                    el.UpdatePath();
                    canvasController.Insert(el);
                    canvasController.DeselectCurrentSelectedElements();
                    canvasController.SelectElement(el);
                    serviceManager.Get<IFlowSharpDebugWindowService>().UpdateDebugWindow();

                    if (!dragging) return;
                    if (el.IsConnector)
                    {
                        el.ShowAnchors = true;
                    }
                },
                () =>
                {
                    // ElementCache.Instance.Add(el);
                    canvasController.DeselectCurrentSelectedElements();
                    canvasController.DeleteElement(el, false);
                    canvasController.SelectElements(currentSelectedShapes);
                    serviceManager.Get<IFlowSharpDebugWindowService>().UpdateDebugWindow();
                });

            return el;
        }

        protected Point GetCanvasPointerLocation(BaseController canvasController)
        {
            return canvasController.Canvas.PointToClient(Cursor.Position);
        }

        protected bool IsPointInsideCanvas(BaseController canvasController, Point point)
        {
            return canvasController.Canvas.ClientRectangle.Contains(point);
        }

        public override void SelectElement(GraphicElement el)
        {
            DeselectCurrentSelectedElement();
            if (el == null) return;

            var els = EraseOurselvesAndIntersectionsTopToBottom(el);
            el.Select();
            DrawBottomToTop(els);
            UpdateScreen(els);
            selectedElements.Add(el);
            ElementSelected.Fire(this, new ElementEventArgs() { Element = el });
        }

        protected GraphicElement GetSelectedElement(Point p)
        {
            return elements.FirstOrDefault(e => e.DisplayRectangle.Contains(p));
        }

        protected void DeselectCurrentSelectedElement()
        {
            if (selectedElements?.Any() != true) return;
            var els = EraseOurselvesAndIntersectionsTopToBottom(selectedElements[0]);
            selectedElements[0].Deselect();
            DrawBottomToTop(els);
            UpdateScreen(els);
            selectedElements.Clear();
        }

        protected void ToolboxCanvasPaintComplete(Canvas canvas)
        {
            Trace.WriteLine("*** ToolboxCanvasPaintComplete");
            eraseCount = 1;         // Diagnostics
            RepositionToolboxElements();
            DrawBottomToTop(elements);
        }

        protected void RepositionToolboxElements()
        {
            var y = 15;
            var x = 15;
            foreach (var el in Elements)
            {
                // standard width/height is 25x25
                el.DisplayRectangle = new Rectangle(new Point(x - (el.DisplayRectangle.Width - 25)/2, y - (el.DisplayRectangle.Height-25)/2), el.DisplayRectangle.Size);

                // TODO: Fix this, so that when we change the display rectangle, or we use some other to-be-created method,
                // the StartPoint/EndPoint works correctly.  Or maybe the StartPoint/EndPoint can always be calculated from the
                // DisplayRectangle?
                // COMMENT: This is probably NOT a good idea because of the side-effect!
                // If we do this, StartPoint and EndPoint should be "getters" only!
                if (el is DynamicConnector dc)
                {
                    dc.StartPoint = el.DisplayRectangle.TopLeftCorner();
                    dc.EndPoint = el.DisplayRectangle.BottomRightCorner();
                }

                el.UpdatePath();
                x += 50;

                if (x + el.DisplayRectangle.Width + 10 > Canvas.Width)
                {
                    y += 50;
                    x = 15;
                }
            }
        }
    }
}
