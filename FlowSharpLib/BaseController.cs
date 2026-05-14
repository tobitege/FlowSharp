/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

using Clifton.Core.ExtensionMethods;

namespace FlowSharpLib
{
    public class ZOrderMap
    {
        public GraphicElement Element { get; set; }
        public int Index { get; set; }
        public List<GraphicElement> GroupChildren { get; set; }
        public List<Connection> Connections { get; set; }

        // For connectors:
        public GraphicElement StartConnectedShape { get; set; }
        public GraphicElement EndConnectedShape { get; set; }
        public Connection StartConnection { get; set; }
        public Connection EndConnection { get; set; }
    }

    public enum OrthogonalConnectorOrientation
    {
        LeftRight,
        UpDown
    }

    public abstract class BaseController
    {
        public event EventHandler<EventArgs> CanvasNameChanged;
        public event EventHandler<EventArgs> FilenameChanged;
        public EventHandler<ElementEventArgs> ElementSelected;
        public EventHandler<ElementEventArgs> UpdateSelectedElement;

        public const int MIN_WIDTH = 20;
        public const int MIN_HEIGHT = 20;

        public const int CONNECTION_POINT_SIZE = 3;		// this is actually the length from center.
        public const int GROUPBOX_INFLATE = 15;

        public const int CAP_WIDTH = 5;
        public const int CAP_HEIGHT = 5;

        public Canvas Canvas => canvas;

        public string Filename
        {
            get => filename;
            set
            {
                filename = value;
                FilenameChanged.Fire(this, EventArgs.Empty);
            }
        }

        public string CanvasName
        {
            get => canvasName;
            set
            {
                canvasName = value;
                CanvasNameChanged.Fire(this, EventArgs.Empty);
            }
        }


        // TODO: Return back to ReadOnlyCollection and implement the functions that the menu controller needs.
        public ReadOnlyCollection<GraphicElement> Elements => elements.AsReadOnly();

        // TODO: Implement as interface
        // public MouseController MouseController { get; set; }

        // TODO: Kludgy workaround for issue #34.
        public bool IsCanvasDragging { get; set; }

        // Ignore snap if Ctrl key is pressed when "doing".
        // Undo/redo is more complicated, especially since it can be activated with a keyboard Ctrl+Z or Ctrl+Y
        // which means the ctrl key is pressed.  Furthermore, the original ignore snap needs to be preserved.
        public bool UndoRedoIgnoreSnapCheck { get; set; }
        public bool IsSnapToBeIgnored => ((Control.ModifierKeys & Keys.Control) == Keys.Control) || UndoRedoIgnoreSnapCheck;

        public UndoStack UndoStack => undoStack;
        public ReadOnlyCollection<GraphicElement> SelectedElements => selectedElements.AsReadOnly();

        public SnapController SnapController { get; protected set; }

        public int Zoom { get; protected set; }
        public Point CanvasOffset { get; protected set; }
        public int RotationSnapDegrees { get; set; }

        protected List<GraphicElement> elements;
        protected Canvas canvas;
        protected UndoStack undoStack;
        protected List<GraphicElement> selectedElements;
        protected string canvasName;
        protected string filename;

        // Diagnostic
        protected int eraseCount;

        public BaseController(Canvas canvas)
        {
            undoStack = new UndoStack();
            this.canvas = canvas;
            elements = new List<GraphicElement>();
            selectedElements = new List<GraphicElement>();
            SnapController = new SnapController(this);
            Zoom = 100;
            CanvasOffset = Point.Empty;
            RotationSnapDegrees = 15;
        }

        public virtual bool IsMultiSelect()
        {
            return !((Control.ModifierKeys & (Keys.Control | Keys.Shift)) == 0);
        }

        // TODO: These empty base class methods are indicative of bad design.
        public virtual void SelectElement(GraphicElement el) { }
        public virtual void SelectOnlyElement(GraphicElement el) { }
        public virtual void SetAnchorCursor(GraphicElement el) { }
        public virtual void DragSelectedElements(Point delta) { }
        public virtual void DeselectCurrentSelectedElements() { }
        public virtual void DeselectGroupedElements() { }
        public virtual void DeselectElement(GraphicElement el) { }

        public void Insert(int idx, GraphicElement el)
        {
            elements.Insert(idx, el);
        }

        public void AddElement(GraphicElement el)
        {
            elements.Add(el);
            UpdateViewport();
        }

        public void AddElements(List<GraphicElement> els)
        {
            elements.AddRange(els);
            UpdateViewport();
        }

        public void SaveChildZOrder(GraphicElement el, List<ZOrderMap> zorder)
        {
            el.GroupChildren.ForEach(gc =>
            {
                ZOrderMap zom = new ZOrderMap() { Element = gc, Index = elements.IndexOf(gc) };
                zom.GroupChildren = new List<GraphicElement>(gc.GroupChildren);
                zom.Connections = new List<Connection>(gc.Connections);
                zorder.Add(zom);
                SaveChildZOrder(gc, zorder);
            });
        }

        // TODO: This does more than just getting the zorder - it also saves connection information for connectors,
        // which is critical to wire up connectors to shapes after a delete has been undone.
        public List<ZOrderMap> GetZOrderOfSelectedElements()
        {
            List<ZOrderMap> originalZOrder = new List<ZOrderMap>();

            selectedElements.ForEach(el =>
            {
                ZOrderMap zom = new ZOrderMap() { Element = el, Index = elements.IndexOf(el) };
                zom.GroupChildren = new List<GraphicElement>(el.GroupChildren);
                zom.Connections = new List<Connection>(el.Connections);

                if (el.IsConnector)
                {
                    zom.StartConnectedShape = ((Connector)el).StartConnectedShape;
                    zom.EndConnectedShape = ((Connector)el).EndConnectedShape;

                    if (zom.StartConnectedShape != null)
                    {
                        // TODO: First or default used because we have a bug, yet to fix, where the shape can have the same connector attached twice!
                        zom.StartConnection = zom.StartConnectedShape.Connections.FirstOrDefault(conn => conn.ToElement == el);
                    }

                    if (zom.EndConnectedShape != null)
                    {
                        // TODO: First or default used because we have a bug, yet to fix, where the shape can have the same connector attached twice!
                        zom.EndConnection = zom.EndConnectedShape.Connections.FirstOrDefault(conn => conn.ToElement == el);
                    }
                }

                originalZOrder.Add(zom);
                SaveChildZOrder(el, originalZOrder);
            });

            return originalZOrder;
        }

        public void Clear()
        {
            elements.ForEach(el => el.Dispose());
            elements.Clear();
            selectedElements.Clear();
            CanvasOffset = Point.Empty;
        }

        public virtual void Undo()
        {
            undoStack.Undo();
        }

        public virtual void Redo()
        {
            undoStack.Redo();
        }

        public bool IsRootShapeSelectable(Point p)
        {
            return GetRootShapesAt(p).Any();
        }

        public bool IsChildShapeSelectable(Point p)
        {
            return GetChildShapesAt(p).Any();
        }

        public GraphicElement GetRootShapeAt(Point p)
        {
            return GetRootShapesAt(p).FirstOrDefault();
        }

        public GraphicElement GetChildShapeAt(Point p)
        {
            return GetChildShapesAt(p).FirstOrDefault();
        }

        public GraphicElement GetSelectableShapeAt(Point p)
        {
            return GetSelectableShapesAt(p).FirstOrDefault();
        }

        public IEnumerable<GraphicElement> GetRootShapesAt(Point p)
        {
            return elements.Where(e => IsSelectableAtDepth(e, p, 0));
        }

        public IEnumerable<GraphicElement> GetChildShapesAt(Point p, int maxDepth = 1)
        {
            return elements.Where(e =>
            {
                int depth = GetSelectionDepth(e);
                return depth >= 1 && depth <= maxDepth && IsSelectableAtDepth(e, p, depth);
            });
        }

        public IEnumerable<GraphicElement> GetSelectableShapesAt(Point p, int maxDepth = 1)
        {
            return GetRootShapesAt(p).Concat(GetChildShapesAt(p, maxDepth));
        }

        public GraphicElement GetNextRootShapeAt(Point p, GraphicElement currentSelection = null)
        {
            var candidates = GetRootShapesAt(p).ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            if (currentSelection == null)
            {
                return candidates[0];
            }

            int idx = candidates.IndexOf(currentSelection);

            return idx < 0 ? candidates[0] : candidates[(idx + 1) % candidates.Count];
        }

        public List<GraphicElement> GetShapesInSelectionRegion(Rectangle selectionRectangle)
        {
            return elements.Where(e => e.Parent == null && e.Visible && e.UpdateRectangle.IntersectsWith(selectionRectangle)).ToList();
        }

        public GroupBox GetContainingGroupBox(GraphicElement el)
        {
            return elements.OfType<GroupBox>()
                .Where(g => g != el && g.Visible && g.DisplayRectangle.Contains(el.DisplayRectangle))
                .OrderBy(g => g.DisplayRectangle.Width * g.DisplayRectangle.Height)
                .FirstOrDefault();
        }

        public void AddShapeToGroup(GroupBox groupBox, GraphicElement el)
        {
            if (groupBox == null || el == null || groupBox == el || el.Parent == groupBox)
            {
                return;
            }

            if (el.Parent is GroupBox currentGroup)
            {
                currentGroup.GroupChildren.Remove(el);
            }

            el.Parent = groupBox;
            groupBox.GroupChildren.AddIfUnique(el);
            canvas.Invalidate();
        }

        public void RemoveShapeFromGroup(GroupBox groupBox, GraphicElement el)
        {
            if (groupBox == null || el == null || el.Parent != groupBox)
            {
                return;
            }

            groupBox.GroupChildren.Remove(el);
            el.Parent = null;
            canvas.Invalidate();
        }

        public void SelectElements(List<GraphicElement> els)
        {
            els.ForEach(el => SelectElement(el));
        }

        // Called when undo'ing a zorder move.
        public void RestoreZOrder(List<ZOrderMap> zorder)
        {
            // Remove all shapes from the elements list.
            zorder.Select(zo => zo.Element).ForEach(el => elements.Remove(el));
            // Insert them into the list in ascending order, so each insertion goes in the right place.
            zorder.OrderBy(zo => zo.Index).ForEach(zo => elements.Insert(zo.Index, zo.Element));
            // TODO: Redraw everything, because I'm lazy and because this actually might be the best way of getting all the pieces to play nice together.
            canvas.Invalidate();
        }

        // Called when undo'ing a UI initiated delete of selected shapes.
        public void RestoreZOrderWithHierarchy(List<ZOrderMap> zorder)
        {
            // Insert them into the list in ascending order, so each insertion goes in the right place.
            zorder.OrderBy(zo => zo.Index).ForEach(zo => elements.Insert(zo.Index, zo.Element));
            zorder.ForEach(zo =>
            {
                zo.Element.Connections = new List<Connection>();
                zo.Connections.ForEach(conn => zo.Element.AddConnection(conn));
            });

            zorder.ForEach(zo =>
            {
                zo.Element.GroupChildren = new List<GraphicElement>(zo.GroupChildren);
                zo.Element.GroupChildren.ForEach(gc => gc.Parent = zo.Element);
            });

            zorder.ForEach(zo => zo.Element.Restored());

            canvas.Invalidate();
        }

        public void Topmost()
        {
            // TODO: Sub-optimal, as we're erasing all elements.
            EraseTopToBottom(elements);

            // In their original z-order, but reversed because we're inserting at the top...
            selectedElements.OrderByDescending(el => elements.IndexOf(el)).ForEach(el =>
            {
                elements.Remove(el);
                elements.Insert(0, el);
                // Preserve child order.
                el.GroupChildren.OrderByDescending(child=>elements.IndexOf(child)).ForEach(child => MoveToTop(child));
            });

            DrawBottomToTop(elements);
            UpdateScreen(elements);
        }

        public void Bottommost()
        {
            // TODO: Sub-optimal, as we're erasing all elements.
            EraseTopToBottom(elements);

            // In their original z-oder, since we're appending to the bottom...
            selectedElements.OrderBy(el => elements.IndexOf(el)).ForEach(el =>
            {
                elements.Remove(el);
                // Preserve child order.
                el.GroupChildren.OrderBy(child=>elements.IndexOf(child)).ForEach(child => MoveToBottom(child));
                elements.Add(el);
            });

            DrawBottomToTop(elements);
            UpdateScreen(elements);
        }

        public void MoveSelectedElementsUp()
        {
            // TODO: Sub-optimal, as we're erasing all elements.
            EraseTopToBottom(elements);
            MoveUp(selectedElements);
            DrawBottomToTop(elements);
            UpdateScreen(elements);
        }

        public void MoveSelectedElementsDown()
        {
            // TODO: Sub-optimal, as we're erasing all elements.
            EraseTopToBottom(elements);
            MoveDown(selectedElements);
            DrawBottomToTop(elements);
            UpdateScreen(elements);
        }

        // Used by UI "delete shape", this is a recursive destruction of shapes and, if they are groupboxes, their child shapes, etc.
        public void DeleteSelectedElementsHierarchy(bool dispose = true)
        {
            // TODO: Optimize for redrawing just selected elements (we remove call to DeleteElement when we do this)
            selectedElements.ForEach(el =>
            {
                DeleteElementHierarchy(el, dispose);
                el.DetachAll();
                el.Connections.ForEach(c => c.ToElement.RemoveConnection(c.ToConnectionPoint.Type));
                el.Connections.Clear();
                RemoveElement(el, dispose);
            });

            selectedElements.Clear();
            canvas.Invalidate();
        }

        // Used by secondary operations, particularly undo events, where we delete things we've pasted or dropped onto the canvas.
        public void DeleteElement(GraphicElement el, bool dispose = true)
        {
            selectedElements.Remove(el);
            el.DetachAll();
            var els = EraseOurselvesAndIntersectionsTopToBottom(el);
            var elsToRedraw = els.ToList();
            elsToRedraw.Remove(el);
            el.Connections.ForEach(c => c.ToElement.RemoveConnection(c.ToConnectionPoint.Type));
            el.Connections.Clear();
            DrawBottomToTop(elsToRedraw);
            UpdateScreen(els);          // Original list, so element that is being deleted is included in the region to update.
            RemoveElement(el, dispose);
        }

        public void Redraw(GraphicElement el, int dx=0, int dy=0)
        {
            var els = EraseOurselvesAndIntersectionsTopToBottom(el, dx, dy);
            DrawBottomToTop(els, dx, dy);
            UpdateScreen(els, dx, dy);
        }

        public void Redraw(GraphicElement el, Action<GraphicElement> afterErase)
        {
            var els = EraseOurselvesAndIntersectionsTopToBottom(el);
            UpdateScreen(els);
            afterErase(el);
            el.UpdatePath();
            DrawBottomToTop(els);
            UpdateScreen(els);
        }

        public void Insert(GraphicElement el)
        {
            elements.Insert(0, el);
            Redraw(el);
            UpdateViewport();
        }

        public GraphicElement InsertAt(GraphicElement prototype, Point clientPoint)
        {
            GraphicElement el = prototype.CloneDefault(canvas);
            Point worldPoint = ClientToWorld(clientPoint);
            Point target = new Point(
                worldPoint.X - el.DisplayRectangle.Width / 2,
                worldPoint.Y - el.DisplayRectangle.Height / 2);
            el.DisplayRectangle = new Rectangle(target, el.DisplayRectangle.Size);
            el.UpdateProperties();
            el.UpdatePath();
            Insert(el);

            return el;
        }

        public void UpdateSize(GraphicElement el, ShapeAnchor anchor, Point delta)
        {
            var adjustedDelta = anchor.AdjustedDelta(InverseZoomAdjust(delta));
            var newRect = anchor.Resize(el.DisplayRectangle, adjustedDelta);
            UpdateDisplayRectangle(el, newRect, adjustedDelta);
            UpdateConnections(el);
            UpdateSelectedElement.Fire(this, new ElementEventArgs() { Element = el });
        }

        /// <summary>
        /// Direct update of display rectangle, used in DynamicConnector.
        /// </summary>
        public void UpdateDisplayRectangle(GraphicElement el, Rectangle newRect, Point delta)
        {
            var dx = delta.X.Abs();
            var dy = delta.Y.Abs();
            var els = EraseOurselvesAndIntersectionsTopToBottom(el, dx, dy);
            el.DisplayRectangle = newRect;
            el.UpdatePath();
            DrawBottomToTop(els, dx, dy);
            UpdateScreen(els, dx, dy);
        }

        public GroupBox GroupShapes(GroupBox groupBox)
        {
            var shapesToGroup = selectedElements;
            groupBox.GroupChildren.AddRange(shapesToGroup);
            var r = GetExtents(shapesToGroup);
            r.Inflate(GROUPBOX_INFLATE, GROUPBOX_INFLATE);
            groupBox.DisplayRectangle = r;
            shapesToGroup.ForEach(s => s.Parent = groupBox);
            var intersections = FindAllIntersections(groupBox).ToList();

            // Also include shapes being grouped so these are erased as well,
            // otherwise they leave an "echo" behind the new grouping.
            shapesToGroup.ForEach(s =>
            {
                if (!intersections.Contains(s))
                {
                    intersections.Add(s);
                }
            });

            EraseTopToBottom(intersections);

            // Insert groupbox just after the lowest shape being grouped.
            var insertionPoint = shapesToGroup.Select(s => elements.IndexOf(s)).OrderBy(n => n).Last() + 1;
            elements.Insert(insertionPoint, groupBox);

            intersections = FindAllIntersections(groupBox).ToList();
            DrawBottomToTop(intersections);
            UpdateScreen(intersections);

            return groupBox;
        }

        public void UngroupShapes(GroupBox el, bool dispose=true)
        {
            List<GraphicElement> intersections = FindAllIntersections(el).ToList();

            // Preserve the original list, including the group boxes, for when we update the screen,
            // So that the erased groupbox region is updated on the screen.
            var originalIntersections = new List<GraphicElement>(intersections);

            el.GroupChildren.ForEach(c => c.Parent = null);
            el.GroupChildren.Clear();
            EraseTopToBottom(intersections.AsEnumerable());

            RemoveElement(el, dispose);
            intersections.Remove(el);

            DrawBottomToTop(intersections.AsEnumerable());
            UpdateScreen(originalIntersections);        // remember, this updates the screen for the now erased groupbox.
        }

        public GroupBox RegroupShapes(GroupBox groupBox, IEnumerable<GraphicElement> shapes)
        {
            DeselectCurrentSelectedElements();
            SelectElements(shapes.Where(shape => shape != groupBox).ToList());

            return GroupShapes(groupBox);
        }

        public void MoveSelectedElements(Point delta, bool snapToCentersAndEdges = false)
        {
            // TODO: We shouldn't even be calling this method if there are no selected elements!
            if (selectedElements.Count == 0) return;

            delta = InverseZoomAdjust(delta);

            if (snapToCentersAndEdges)
            {
                Point snapDelta = GetSelectedCenterEdgeSnapDelta(delta);
                delta = new Point(delta.X + snapDelta.X, delta.Y + snapDelta.Y);
            }

            var dx = delta.X.Abs();
            var dy = delta.Y.Abs();
            var intersections = new List<GraphicElement>();

            selectedElements.ForEach(el =>
            {
                intersections.AddRange(FindAllIntersections(el));
            });

            var distinctIntersections = intersections.Distinct().ToList();
            //var connectors = new List<GraphicElement>();

            //selectedElements.ForEach(el =>
            //{
            //    el.Connections.ForEach(c =>
            //    {
            //        if (!connectors.Contains(c.ToElement))
            //        {
            //            connectors.Add(c.ToElement);
            //        }
            //    });
            //});

            EraseTopToBottom(distinctIntersections);

            //connectors.ForEach(c =>
            //{
            //    // X1
            //    //c.MoveUndoRedo(delta, false);
            //    c.Move(delta);
            //    c.UpdatePath();
            //});

            selectedElements.ForEach(el =>
            {
                // TODO: Kludgy workaround for dealing with multiple shape dragging with connectors in the selection list.
                // if (!el.IsConnector)
                {
                    // X1
                    //el.MoveUndoRedo(delta, false);
                    el.Move(delta);
                    el.UpdatePath();
                    // Issue #49 - multiple selected shapes don't move anchors/lines of connectors connectors/lines.
                    el.Connections.ForEach(c =>
                    {
                        if (!selectedElements.Contains(c.ToElement))
                        {
                            MoveLineOrAnchor(c, delta);
                        }
                    });
                }
            });

            DrawBottomToTop(distinctIntersections, dx, dy);
            UpdateScreen(distinctIntersections, dx, dy);
            UpdateSelectedElement.Fire(this, new ElementEventArgs() { Element = selectedElements[0] });
        }

        public void MoveLineOrAnchor(Connection c, Point delta)
        {
            if (c.ToElement is DynamicConnector dynamicConnector &&
                (dynamicConnector.StartConnectedShape != null || dynamicConnector.EndConnectedShape != null))
            {
                dynamicConnector.AutoAnchor();
            }
            else if (c.ToElement is Line)
            {
                c.ToElement.Move(delta);
            }
            else
            {
                c.ToElement.MoveAnchor(c.ToConnectionPoint.Type, delta);
            }
        }

        public DynamicConnector ConvertConnectorToOrthogonal(Connector connector, OrthogonalConnectorOrientation orientation)
        {
            if (!(connector is DynamicConnector source))
            {
                throw new ArgumentException("Only dynamic connectors can be converted.", nameof(connector));
            }

            if ((orientation == OrthogonalConnectorOrientation.LeftRight && connector is DynamicConnectorLR) ||
                (orientation == OrthogonalConnectorOrientation.UpDown && connector is DynamicConnectorUD))
            {
                return source;
            }

            DynamicConnector replacement = orientation == OrthogonalConnectorOrientation.LeftRight
                ? new DynamicConnectorLR(canvas, source.StartPoint, source.EndPoint)
                : new DynamicConnectorUD(canvas, source.StartPoint, source.EndPoint);

            CopyConnectorProperties(source, replacement);
            ReplaceConnector(source, replacement);

            return replacement;
        }

        public int RemoveDiagonalConnectors()
        {
            var diagonals = elements.OfType<DiagonalConnector>().ToList();

            diagonals.ForEach(connector =>
            {
                connector.DetachAll();
                selectedElements.Remove(connector);
                RemoveElement(connector, true);
            });

            if (diagonals.Any())
            {
                canvas.Invalidate();
            }

            return diagonals.Count;
        }

        public void MoveElement(GraphicElement el, Point delta)
        {
            if (el.OnScreen())
            {
                var dx = delta.X.Abs();
                var dy = delta.Y.Abs();
                var els = EraseOurselvesAndIntersectionsTopToBottom(el, dx, dy);
                el.Move(delta);
                el.UpdatePath();
                UpdateConnections(el);
                DrawBottomToTop(els, dx, dy);
                UpdateScreen(els, dx, dy);
            }
            else
            {
                el.CancelBackground();
                el.Move(delta);
                // TODO: Display element if moved back on screen at this point?
            }
        }

        protected void ReplaceConnector(DynamicConnector source, DynamicConnector replacement)
        {
            int index = elements.IndexOf(source);
            if (index < 0)
            {
                throw new InvalidOperationException("Connector is not on this controller.");
            }

            elements[index] = replacement;

            elements.ForEach(el =>
            {
                el.Connections.Where(connection => connection.ToElement == source).ForEach(connection =>
                {
                    connection.ToElement = replacement;
                });
            });

            bool wasSelected = selectedElements.Remove(source);
            source.Deselect();

            if (wasSelected)
            {
                selectedElements.Add(replacement);
                replacement.Select();
            }

            replacement.UpdateProperties();
            replacement.UpdatePath();
            replacement.DisplayRectangle = replacement.DisplayRectangle == Rectangle.Empty
                ? replacement.DefaultRectangle()
                : replacement.DisplayRectangle;
            source.Removed(true);
            source.Dispose();
            UpdateViewport();
            canvas.Invalidate();
        }

        protected void CopyConnectorProperties(DynamicConnector source, DynamicConnector replacement)
        {
            replacement.Name = source.Name;
            replacement.Text = source.Text;
            replacement.TextColor = source.TextColor;
            replacement.TextAlign = source.TextAlign;
            replacement.Multiline = source.Multiline;
            replacement.WordWrap = source.WordWrap;
            replacement.TextBounds = source.TextBounds;
            replacement.TextMargin = source.TextMargin;
            replacement.ParagraphJustification = source.ParagraphJustification;
            replacement.LabelOffset = source.LabelOffset;
            replacement.LabelSize = source.LabelSize;
            replacement.BorderPenColor = source.BorderPenColor;
            replacement.BorderPenWidth = source.BorderPenWidth;
            replacement.FillColor = source.FillColor;
            replacement.StartCap = source.StartCap;
            replacement.EndCap = source.EndCap;
            replacement.StartConnectedShape = source.StartConnectedShape;
            replacement.EndConnectedShape = source.EndConnectedShape;
            replacement.CustomConnectionPoints = source.CustomConnectionPoints.ToList();
            replacement.Json = new Dictionary<string, string>(source.Json);
            replacement.RotationAngle = source.RotationAngle;
            replacement.TextFont.Dispose();
            replacement.TextFont = (Font)source.TextFont.Clone();
        }

        public void MoveElementTo(GraphicElement el, Point location)
        {
            var delta = new Point(location.X - el.DisplayRectangle.Left, location.Y - el.DisplayRectangle.Top);

            if (el.OnScreen())
            {
                var dx = delta.X.Abs();
                var dy = delta.Y.Abs();
                var els = EraseOurselvesAndIntersectionsTopToBottom(el, dx, dy);
                el.Move(delta);
                el.UpdatePath();
                UpdateConnections(el);
                DrawBottomToTop(els, dx, dy);
                UpdateScreen(els, dx, dy);
            }
            else
            {
                el.CancelBackground();
                el.Move(delta);
                // TODO: Display element if moved back on screen at this point?
            }
        }

        // For canvas dragging.
        public void MoveAllElements(Point delta)
        {
            MoveAllElementsByCanvasDelta(InverseZoomAdjust(delta));
        }

        public void SetCanvasOffset(Point offset)
        {
            MoveAllElementsByCanvasDelta(new Point(offset.X - CanvasOffset.X, offset.Y - CanvasOffset.Y));
        }

        public Point ClientToWorld(Point p)
        {
            return canvas.ClientToWorld(p, Zoom);
        }

        public Point WorldToClient(Point p)
        {
            return canvas.WorldToClient(p, Zoom);
        }

        public Point InverseZoomAdjust(Point p)
        {
            var ret = p;
            if (Zoom != 100)
            {
                ret = new Point(p.X * 100 / Zoom, p.Y * 100 / Zoom);
            }

            return ret;
        }

        public void SetZoom(int zoom)
        {
            zoom = Math.Max(10, Math.Min(zoom, 400));
            // erase and ppdate the screen, removing elements after erasure, so they don't leave their images when the zoom factor is changed.
            EraseTopToBottom(elements);
            UpdateScreen(elements);

            Zoom = zoom;
            UpdateViewport();

            DrawBottomToTop(elements);
            UpdateScreen(elements);
        }

        public void UpdateViewport()
        {
            elements.ForEach(e =>
            {
                e.UpdateZoomRectangle();
                e.UpdatePath();
            });
            canvas.UpdateScrollbars(GetDiagramExtents(), Zoom);
        }

        public Rectangle GetDiagramExtents()
        {
            Rectangle extents = canvas.PageBounds;

            if (elements.Count == 0)
            {
                return extents;
            }

            elements.ForEach(e => extents = extents.Union(e.DisplayRectangle));

            return extents;
        }

        public void AlignSelected(GripType alignment)
        {
            if (selectedElements.Count < 2)
            {
                return;
            }

            switch (alignment)
            {
                case GripType.LeftMiddle:
                    AlignSelectedByOffset(e => e.DisplayRectangle.Left, values => values.Min(), (e, offset) => new Point(offset, 0));
                    break;

                case GripType.RightMiddle:
                    AlignSelectedByOffset(e => e.DisplayRectangle.Right, values => values.Max(), (e, offset) => new Point(offset, 0));
                    break;

                case GripType.TopMiddle:
                    AlignSelectedByOffset(e => e.DisplayRectangle.Top, values => values.Min(), (e, offset) => new Point(0, offset));
                    break;

                case GripType.BottomMiddle:
                    AlignSelectedByOffset(e => e.DisplayRectangle.Bottom, values => values.Max(), (e, offset) => new Point(0, offset));
                    break;
            }
        }

        public Point GetCenterEdgeSnapDelta(GraphicElement movingElement, int range = 5)
        {
            return GetCenterEdgeSnapDelta(movingElement, Point.Empty, range);
        }

        public Point GetCenterEdgeSnapDelta(GraphicElement movingElement, Point proposedDelta, int range = 5)
        {
            var candidates = elements.Where(e => e != movingElement && e.Visible).ToList();

            if (!candidates.Any())
            {
                return Point.Empty;
            }

            var moving = movingElement.DisplayRectangle.Move(proposedDelta);
            return GetCenterEdgeSnapDelta(moving, candidates, range);
        }

        protected Point GetSelectedCenterEdgeSnapDelta(Point proposedDelta, int range = 5)
        {
            var movingElements = selectedElements.Where(e => !e.IsConnector && e.Visible).ToList();

            if (!movingElements.Any())
            {
                return Point.Empty;
            }

            var candidates = elements.Where(e => !movingElements.Contains(e) && e.Visible).ToList();

            if (!candidates.Any())
            {
                return Point.Empty;
            }

            Rectangle moving = GetExtents(movingElements).Move(proposedDelta);
            return GetCenterEdgeSnapDelta(moving, candidates, range);
        }

        protected Point GetCenterEdgeSnapDelta(Rectangle moving, IEnumerable<GraphicElement> candidates, int range)
        {
            var movingXs = new[] { moving.Left, moving.Center().X, moving.Right };
            var movingYs = new[] { moving.Top, moving.Center().Y, moving.Bottom };
            var targetXs = candidates.SelectMany(e => new[] { e.DisplayRectangle.Left, e.DisplayRectangle.Center().X, e.DisplayRectangle.Right });
            var targetYs = candidates.SelectMany(e => new[] { e.DisplayRectangle.Top, e.DisplayRectangle.Center().Y, e.DisplayRectangle.Bottom });
            int dx = FindNearestSnapDelta(movingXs, targetXs, range);
            int dy = FindNearestSnapDelta(movingYs, targetYs, range);

            return new Point(dx, dy);
        }

        public void RotateSelected(int degrees)
        {
            if (selectedElements.Count == 0)
            {
                return;
            }

            selectedElements.ForEach(el =>
            {
                el.RotationAngle = NormalizeAngle(el.RotationAngle + SnapRotation(degrees));
                Redraw(el);
            });
        }

        protected void MoveAllElementsByCanvasDelta(Point delta)
        {
            if (delta == Point.Empty)
            {
                return;
            }

            var rootElements = elements.Where(e => e.Parent == null).ToList();

            if (!rootElements.Any())
            {
                return;
            }

            if (IsCanvasDragging)
            {
                Trace.WriteLine("*** MoveAllElements Collision");
            }

            IsCanvasDragging = true;            // Kludgy workaround for Issue #34 (groupbox update)
            EraseTopToBottom(elements);

            // Don't move grouped children, as the groupbox will do this for us when it moves.
            rootElements.ForEach(e =>
            {
                e.Move(delta);
                e.UpdatePath();
            });

            CanvasOffset = new Point(CanvasOffset.X + delta.X, CanvasOffset.Y + delta.Y);

            var dx = delta.X.Abs();
            var dy = delta.Y.Abs();
            DrawBottomToTop(elements, dx, dy);
            UpdateScreen(elements, dx, dy);
            IsCanvasDragging = false;
        }

        public void RedrawAllElements()
        {
            EraseTopToBottom(elements);

            // Don't move grouped children, as the groupbox will do this for us when it moves.
            elements.Where(e => e.Parent == null).ForEach(e => e.UpdatePath());
            DrawBottomToTop(elements);
            UpdateScreen(elements);
        }

        public void SaveAsPng(string fname, bool selectionOnly = false)
        {
            selectionOnly.If(() => SaveAsPng(fname, SelectedElements.ToList())).Else(() => SaveAsPng(fname, elements));
        }

        public void RenderTo(Graphics targetGraphics, Rectangle targetBounds, bool selectionOnly = false)
        {
            var elems = selectionOnly ? SelectedElements.ToList() : elements;

            if (!elems.Any())
            {
                return;
            }

            Rectangle extents = GetExtents(elems);
            float scale = Math.Min((float)targetBounds.Width / extents.Width, (float)targetBounds.Height / extents.Height);
            int previousZoom = Zoom;

            canvas.UseViewportOrigin(Point.Empty, () =>
            {
                Zoom = 100;
                elems.ForEach(e =>
                {
                    e.UpdateZoomRectangle();
                    e.UpdatePath();
                });

                var state = targetGraphics.Save();
                try
                {
                    targetGraphics.TranslateTransform(targetBounds.Left - extents.Left * scale, targetBounds.Top - extents.Top * scale);
                    targetGraphics.ScaleTransform(scale, scale);

                    elems.AsEnumerable().Reverse().ForEach(e =>
                    {
                        e.Draw(targetGraphics, false);
                        e.DrawText(targetGraphics);
                    });
                }
                finally
                {
                    targetGraphics.Restore(state);
                    Zoom = previousZoom;
                    elems.ForEach(e =>
                    {
                        e.UpdateZoomRectangle();
                        e.UpdatePath();
                    });
                }
            });
        }

        public PrintDocument CreatePrintDocument(bool selectionOnly = false)
        {
            var document = new PrintDocument();
            document.PrintPage += (sender, args) =>
            {
                RenderTo(args.Graphics, args.MarginBounds, selectionOnly);
                args.HasMorePages = false;
            };

            return document;
        }

        protected void SaveAsPng(string fname, List<GraphicElement> elems)
        {
            // Get boundaries of of all elements.
            var x1 = elems.Min(e => e.DisplayRectangle.X);
            var y1 = elems.Min(e => e.DisplayRectangle.Y);
            var x2 = elems.Max(e => e.DisplayRectangle.X + e.DisplayRectangle.Width);
            var y2 = elems.Max(e => e.DisplayRectangle.Y + e.DisplayRectangle.Height);
            var w = x2 - x1 + 10;
            var h = y2 - y1 + 10;
            var pngCanvas = new Canvas();
            pngCanvas.CreateBitmap(w, h);
            var gr = pngCanvas.AntiAliasGraphics;

            gr.Clear(Color.White);
            var offset = new Point(-(x1-5), -(y1-5));
            var restore = new Point(x1-5, y1-5);

            elems.AsEnumerable().Reverse().ForEach(e =>
            {
                e.Move(offset);
                e.UpdatePath();
                // ReSharper disable once AccessToDisposedClosure
                e.SetCanvas(pngCanvas);
                e.Draw(gr, false);      // Don't draw selection or tag shapes.
                e.DrawText(gr);
                e.SetCanvas(canvas);
                e.Move(restore);
                e.UpdatePath();
            });

            pngCanvas.Bitmap.Save(fname, System.Drawing.Imaging.ImageFormat.Png);
            pngCanvas.Dispose();
        }

        public IEnumerable<GraphicElement> FindAllIntersections(GraphicElement el, int dx = 0, int dy = 0)
        {
            var intersections = new List<GraphicElement>();
            RecursiveFindAllIntersections(intersections, el, dx, dy);

            return intersections.OrderBy(e => elements.IndexOf(e));
        }

        public void EraseTopToBottom(IEnumerable<GraphicElement> els)
        {
            if (++eraseCount > 1)
            {
            }

            els.Where(e => e.OnScreen()).ForEach(e => e.Erase());
        }

        public void DrawBottomToTop(IEnumerable<GraphicElement> els, int dx = 0, int dy = 0)
        {
            if (--eraseCount < 0)
            {
            }

            els.AsEnumerable().Reverse().Where(e => e.OnScreen(dx, dy)).ForEach(e =>
            {
                e.GetBackground();
                e.Draw();
            });
        }

        public void UpdateScreen(IEnumerable<GraphicElement> els, int dx = 0, int dy = 0)
        {
            // Is this faster than creating a unioned rectangle?  Dunno, because the unioned rectangle might include a lot of space not part of the shapes, like something in an "L" pattern.
            els.Where(e => e.OnScreen(dx, dy)).ForEach(e => e.UpdateScreen(dx, dy));
        }

        /// <summary>
        /// Center the canvas on the selected element.
        /// This does not add the action to the undo stack.
        /// </summary>
        public void FocusOn(GraphicElement el)
        {
            Point center = el.DisplayRectangle.Center();
            Point scaledCenter = new Point(center.X * Zoom / 100, center.Y * Zoom / 100);
            Canvas.SetViewportOrigin(scaledCenter.X - Canvas.Width / 2, scaledCenter.Y - Canvas.Height / 2);
        }

        public void ClearBookmarks()
        {
            Elements.ForEach(el =>
            {
                el.ClearBookmark();
                Redraw(el);
            });
        }

        protected virtual void RemoveElement(GraphicElement el, bool dispose)
        {
            elements.Remove(el);
            el.Removed(dispose);
            UpdateViewport();

            if (dispose)
            {
                el.Dispose();
            }
        }

        protected bool IsSelectableAtDepth(GraphicElement element, Point p, int depth)
        {
            return element.Visible && GetSelectionDepth(element) == depth && element.IsSelectable(p);
        }

        protected int GetSelectionDepth(GraphicElement element)
        {
            int depth = 0;
            GraphicElement parent = element.Parent;

            while (parent != null)
            {
                depth++;
                parent = parent.Parent;
            }

            return depth;
        }

        protected void AlignSelectedByOffset(Func<GraphicElement, int> valueSelector, Func<IEnumerable<int>, int> targetSelector, Func<GraphicElement, int, Point> deltaFactory)
        {
            var target = targetSelector(selectedElements.Select(valueSelector));
            selectedElements.ForEach(el => MoveElement(el, deltaFactory(el, target - valueSelector(el))));
        }

        protected static int FindNearestSnapDelta(IEnumerable<int> movingValues, IEnumerable<int> targetValues, int range)
        {
            int best = 0;
            int bestAbs = range + 1;

            foreach (int movingValue in movingValues)
            {
                foreach (int targetValue in targetValues)
                {
                    int delta = targetValue - movingValue;
                    int abs = delta.Abs();

                    if (abs < bestAbs)
                    {
                        bestAbs = abs;
                        best = delta;
                    }
                }
            }

            return bestAbs <= range ? best : 0;
        }

        protected int SnapRotation(int degrees)
        {
            if (RotationSnapDegrees <= 0)
            {
                return degrees;
            }

            return (int)Math.Round((double)degrees / RotationSnapDegrees) * RotationSnapDegrees;
        }

        protected static int NormalizeAngle(int angle)
        {
            int ret = angle % 360;

            return ret < 0 ? ret + 360 : ret;
        }

        protected void DeleteElementHierarchy(GraphicElement el, bool dispose)
        {
            el.GroupChildren.ForEach(gc =>
            {
                DeleteElementHierarchy(gc, dispose);
                gc.DetachAll();
                gc.Connections.ForEach(c => c.ToElement.RemoveConnection(c.ToConnectionPoint.Type));
                gc.Connections.Clear();
                RemoveElement(gc, dispose);
            });
        }

        protected void MoveToTop(GraphicElement el)
        {
            elements.Remove(el);
            elements.Insert(0, el);
            el.GroupChildren.ForEach(MoveToTop);
        }

        protected void MoveToBottom(GraphicElement el)
        {
            elements.Remove(el);
            el.GroupChildren.ForEach(MoveToBottom);
            elements.Add(el);
        }

        // The reason for the complexity here in MoveUp/MoveDown is because we're not actually "containing" child elements
        // of a group box in a sub-list.  All child elements are actually part of the master, flat, z-ordered list of shapes (elements.)
        // This means we have to go through some interested machinations to properly move nested groupboxes, however the interesting
        // side effect to this is that, a non-grouped shape, can slide between shapes in a groupbox!

        protected void MoveUp(IEnumerable<GraphicElement> els)
        {
            // Since we're swapping up, order by z-order so we're always swapping with the element above,
            // thus preserving z-order of the selected shapes.

            // (from el in els select new { El = el, Idx = elements.IndexOf(el) }).OrderBy(item => item.Idx).ForEach(item =>
            els.OrderBy(el=>elements.IndexOf(el)).ForEach(el=>
            {
                // To handle groupboxes:
                // 1. Recursively get the list of all grouped shapes, which including sub-groups
                var childElements = new List<GraphicElement>();
                RecursiveGetAllGroupedShapes(el.GroupChildren, childElements);
                childElements = childElements.OrderBy(e => elements.IndexOf(e)).ToList();

                // 2. Delete all those elements, so we are working with root level shapes only.
                childElements.ForEach(child => elements.Remove(child));

                // 3. Now see if there's something to do.
                var idx = elements.IndexOf(el);
                var targetIdx = idx > 0 ? idx - 1 : idx;

                if (targetIdx != idx)
                {
                    elements.Swap(idx, idx - 1);
                }

                // 4. Insert the child elements above the element we just moved up, in reverse order.
                childElements.AsEnumerable().Reverse().ForEach(child => elements.Insert(targetIdx, child));
            });
        }

        protected void MoveDown(IEnumerable<GraphicElement> els)
        {
            // Since we're swapping down, order by z-oder descending so we're always swapping with the element below,
            // thus preserving z-order of the selected shapes.
            els.OrderByDescending(e => elements.IndexOf(e)).ForEach(el =>
            {
                // To handle groupboxes:
                // 1. Recursively get the list of all grouped shapes, which including sub-groups
                var childElements = new List<GraphicElement>();
                RecursiveGetAllGroupedShapes(el.GroupChildren, childElements);
                childElements = childElements.OrderBy(e => elements.IndexOf(e)).ToList();

                // 2. Delete all those elements, so we are working with root level shapes only.
                childElements.ForEach(child => elements.Remove(child));

                // 3. Now see if there's something to do.
                var idx = elements.IndexOf(el);
                var targetIdx = idx < elements.Count - 1 ? idx + 1 : idx;

                if (targetIdx != idx)
                {
                    elements.Swap(idx, idx + 1);
                }

                // 4. Insert the child elements above the element we just moved down, in reverse order.
                childElements.AsEnumerable().Reverse().ForEach(child => elements.Insert(targetIdx, child));
            });
        }

        protected void RecursiveGetAllGroupedShapes(List<GraphicElement> children, List<GraphicElement> acc)
        {
            var pending = new Stack<GraphicElement>(children);
            var seen = new HashSet<GraphicElement>(acc);

            while (pending.Count > 0)
            {
                var child = pending.Pop();

                if (!seen.Add(child))
                {
                    continue;
                }

                acc.Add(child);
                child.GroupChildren.ForEach(pending.Push);
            }
        }

        public Rectangle GetExtents(List<GraphicElement> elems)
        {
            var r = elems.First().DisplayRectangle;
            elems.Skip(1).ForEach(el => r = r.Union(el.DisplayRectangle));
            return r;
        }

        public void UpdateConnections(GraphicElement el)
        {
            HashSet<DynamicConnector> reroutedConnectors = new HashSet<DynamicConnector>();

            el.Connections.ForEach(c =>
            {
                if (c.ToElement is DynamicConnector dynamicConnector &&
                    (dynamicConnector.StartConnectedShape != null || dynamicConnector.EndConnectedShape != null))
                {
                    if (reroutedConnectors.Add(dynamicConnector))
                    {
                        dynamicConnector.AutoAnchor();
                    }

                    return;
                }

                // Connection point on shape.
                var cps = el.GetConnectionPoints().Where(cp2 => cp2.Type == c.ElementConnectionPoint.Type);
                cps.ForEach(cp => c.ToElement.MoveAnchor(cp, c.ToConnectionPoint));
            });
        }

        /// <summary>
        /// Recursive loop to get all intersecting rectangles, including intersectors of the intersectees, so that all elements that
        /// are affected by an overlap redraw are erased and redrawn, otherwise we get artifacts of some intersecting elements when intersection count > 2.
        /// </summary>
        private void RecursiveFindAllIntersections(List<GraphicElement> intersections, GraphicElement el, int dx = 0, int dy = 0)
        {
            var pending = new Stack<Tuple<GraphicElement, int, int>>();
            var seen = new HashSet<GraphicElement>(intersections);
            pending.Push(new Tuple<GraphicElement, int, int>(el, dx, dy));

            while (pending.Count > 0)
            {
                var item = pending.Pop();
                var currentElement = item.Item1;
                var currentDx = item.Item2;
                var currentDy = item.Item3;
                var currentElementIdx = elements.IndexOf(currentElement);
                var rectExpanded = currentElement.UpdateRectangle.Grow(currentDx, currentDy);
                var currentConnections = currentElement.Connections.Select(c => c.ToElement).ToList();

                // Cool thing here is that if the element has no intersections, this list still returns that element because it intersects with itself!
                // Optimization here is that we only collect shapes that intersect and are above (on top of) the current shape.
                // This optimization works really well except that it has a bug, that shapes above connectors in the z-order do not
                // redraw the attached connector.
                var candidates = elements.Where(e =>
                    !seen.Contains(e) &&
                    (elements.IndexOf(e) <= currentElementIdx ||
                    currentConnections.Contains(e)) &&
                    e.UpdateRectangle.IntersectsWith(rectExpanded)).ToList();

                candidates.ForEach(e =>
                {
                    seen.Add(e);
                    intersections.Add(e);
                    pending.Push(new Tuple<GraphicElement, int, int>(e, 0, 0));
                });
            }
        }

        protected List<GraphicElement> EraseOurselvesAndIntersectionsTopToBottom(GraphicElement el, int dx = 0, int dy = 0)
        {
            if (++eraseCount > 1)
            {
            }

            var intersections = FindAllIntersections(el, dx, dy).ToList();
            intersections.AddIfUnique(el);
            intersections.Where(e => e.OnScreen(dx, dy)).OrderBy(e => elements.IndexOf(e)).ForEach(e => e.Erase());

            return intersections;
        }

        // ReSharper disable once ParameterHidesMember
        protected void CanvasPaintComplete(Canvas canvas)
        {
            eraseCount = 1;         // Diagnostics
            DrawBottomToTop(elements);
        }
    }
}
