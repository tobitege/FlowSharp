/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

using Newtonsoft.Json;

using Clifton.Core.ExtensionMethods;
using Clifton.Core.Semantics;
using Clifton.WinForm.ServiceInterfaces;

using FlowSharpLib;
using FlowSharpServiceInterfaces;
using GroupBoxShape = FlowSharpLib.GroupBox;

namespace FlowSharpRestService
{
    public partial class CommandProcessor
    {
        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdNewCanvas cmd)
        {
            var canvasService = proc.ServiceManager.Get<IFlowSharpCanvasService>();

            RunOnUiThread(proc.ServiceManager, () =>
            {
                canvasService.RequestNewCanvas();

                if (!string.IsNullOrWhiteSpace(cmd.Name) && canvasService.ActiveController != null)
                {
                    canvasService.ActiveController.CanvasName = cmd.Name;
                }
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdListCanvases cmd)
        {
            cmd.CanvasesJson = RunOnUiThread(proc.ServiceManager, () =>
                JsonConvert.SerializeObject(GetCanvasReferences(proc.ServiceManager).Select(c => BuildCanvasSummary(proc.ServiceManager, c))));
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdUseCanvas cmd)
        {
            RunOnUiThread(proc.ServiceManager, () =>
            {
                var canvas = ResolveCanvasReference(proc.ServiceManager, cmd);
                if (canvas == null)
                {
                    throw new InvalidOperationException("Canvas was not found.");
                }

                ActivateCanvas(proc.ServiceManager, canvas);
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdSaveWorkspace cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd.Filename))
            {
                throw new InvalidOperationException("Filename is required.");
            }

            var filename = ResolvePath(cmd.Filename);
            var canvasService = proc.ServiceManager.Get<IFlowSharpCanvasService>();
            var editService = proc.ServiceManager.Get<IFlowSharpEditService>();

            RunOnUiThread(proc.ServiceManager, () =>
            {
                if (!cmd.SelectionOnly && cmd.RebaseFilenames)
                {
                    canvasService.RebaseFilenamesOnNextSave();
                }

                canvasService.SaveDiagramsAndLayout(filename, cmd.SelectionOnly);

                if (!cmd.SelectionOnly)
                {
                    editService.SetSavePoint();
                }
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdGetCanvasView cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            cmd.ViewJson = RunOnUiThread(controller, () =>
                JsonConvert.SerializeObject(BuildCanvasViewSummary(controller)));
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdSetZoom cmd)
        {
            if (cmd.Zoom <= 0)
            {
                throw new InvalidOperationException("Zoom must be greater than zero.");
            }

            var controller = GetRequiredActiveController(proc.ServiceManager);

            RunOnUiThread(controller, () => controller.SetZoom(cmd.Zoom));
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdSetCanvasOffset cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            RunOnUiThread(controller, () =>
            {
                Point current = controller.CanvasOffset;
                int targetX;
                int targetY;

                if (cmd.Relative)
                {
                    targetX = current.X + (cmd.Dx ?? cmd.X ?? 0);
                    targetY = current.Y + (cmd.Dy ?? cmd.Y ?? 0);
                }
                else
                {
                    if (!cmd.X.HasValue && !cmd.Dx.HasValue && !cmd.Y.HasValue && !cmd.Dy.HasValue)
                    {
                        throw new InvalidOperationException("Specify X/Y for an absolute offset or Dx/Dy with Relative=true.");
                    }

                    targetX = cmd.X ?? cmd.Dx ?? current.X;
                    targetY = cmd.Y ?? cmd.Dy ?? current.Y;
                }

                controller.SetCanvasOffset(new Point(targetX, targetY));
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdExportPng cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd.Filename))
            {
                throw new InvalidOperationException("Filename is required.");
            }

            var controller = GetRequiredActiveController(proc.ServiceManager);
            var filename = ResolvePath(cmd.Filename);

            RunOnUiThread(controller, () =>
            {
                if (cmd.SelectionOnly)
                {
                    EnsureSelection(controller, "export");
                }

                controller.SaveAsPng(filename, cmd.SelectionOnly);
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdRenderPrintPage cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd.Filename))
            {
                throw new InvalidOperationException("Filename is required.");
            }

            if (cmd.Width <= 0 || cmd.Height <= 0)
            {
                throw new InvalidOperationException("Width and Height must be greater than zero.");
            }

            var controller = GetRequiredActiveController(proc.ServiceManager);
            var filename = ResolvePath(cmd.Filename);

            RunOnUiThread(controller, () =>
            {
                if (cmd.SelectionOnly)
                {
                    EnsureSelection(controller, "render print page");
                }

                using var bitmap = new Bitmap(cmd.Width, cmd.Height);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.White);

                int margin = Math.Max(0, cmd.Margin);
                int printableWidth = Math.Max(1, cmd.Width - margin * 2);
                int printableHeight = Math.Max(1, cmd.Height - margin * 2);
                controller.RenderTo(graphics, new Rectangle(margin, margin, printableWidth, printableHeight), cmd.SelectionOnly);
                bitmap.Save(filename, System.Drawing.Imaging.ImageFormat.Png);
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdSetShapeProperty cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd.PropertyName))
            {
                throw new InvalidOperationException("PropertyName is required.");
            }

            var controller = GetRequiredActiveController(proc.ServiceManager);

            cmd.ResultJson = RunOnUiThread(controller, () =>
            {
                var targets = FindShapes(controller, cmd.Id, cmd.Name, cmd.Text, cmd.Type, cmd.IncludeConnectors, cmd.IncludeChildren)
                    .ToList();

                if (!targets.Any())
                {
                    throw new InvalidOperationException("No shapes matched the property target criteria.");
                }

                if (!cmd.All)
                {
                    targets = targets.Take(1).ToList();
                }

                var changes = targets.Select(el => CreatePropertyChange(el, cmd.PropertyName, cmd.Value)).ToList();
                var redrawModes = changes.Select(change => change.RedrawMode.ToString()).Distinct().ToList();
                string redrawMode = redrawModes.Count == 1 ? redrawModes[0] : "Mixed";

                changes.ForEach(change =>
                {
                    controller.UndoStack.UndoRedo(
                        "RuntimeUpdate " + change.PropertyName,
                        () => ApplyRuntimePropertyChange(controller, change.Element, change.PropertyName, change.NewValue, change.RedrawMode, change.UseElementProperty),
                        () => ApplyRuntimePropertyChange(controller, change.Element, change.PropertyName, change.OldValue, change.RedrawMode, change.UseElementProperty),
                        false);
                });
                controller.UndoStack.FinishGroup();

                return JsonConvert.SerializeObject(new PropertyCommandResult
                {
                    Count = changes.Count,
                    PropertyName = changes[0].PropertyName,
                    RedrawMode = redrawMode,
                    Value = FormatInspectableValue(changes[0].NewValue)
                });
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdSelectShapes cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            RunOnUiThread(controller, () =>
            {
                var matches = FindShapes(controller, cmd.Id, cmd.Name, cmd.Text, cmd.Type, cmd.IncludeConnectors, cmd.IncludeChildren)
                    .ToList();

                if (!matches.Any())
                {
                    throw new InvalidOperationException("No shapes matched the selection criteria.");
                }

                if (!cmd.All)
                {
                    matches = matches.Take(1).ToList();
                }

                ApplySelection(controller, matches, ResolveSelectionMode(cmd.Mode));
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdSelectRegion cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            RunOnUiThread(controller, () =>
            {
                var rect = NormalizeRectangle(cmd.X, cmd.Y, cmd.Width, cmd.Height);
                var matches = controller.GetShapesInSelectionRegion(rect);
                ApplySelection(controller, matches, ResolveSelectionMode(cmd.Mode));
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdGetSelection cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            if (controller == null)
            {
                cmd.SelectionJson = "[]";
                return;
            }

            cmd.SelectionJson = RunOnUiThread(controller, () =>
                JsonConvert.SerializeObject(controller.SelectedElements.Select(el => BuildShapeSummary(controller, el))));
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdMoveSelection cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            RunOnUiThread(controller, () =>
            {
                EnsureSelection(controller, "move");
                var targets = controller.SelectedElements.ToList();
                var delta = new Point(cmd.Dx, cmd.Dy);

                if (cmd.SnapToCentersAndEdges)
                {
                    MoveSelectionWithCapturedUndo(controller, targets, delta, true, "RemoteMoveSelection");
                }
                else
                {
                    controller.UndoStack.UndoRedo(
                        "RemoteMoveSelection",
                        () => MoveCapturedSelection(controller, targets, delta),
                        () => MoveCapturedSelection(controller, targets, Reverse(delta)),
                        true,
                        () => MoveCapturedSelection(controller, targets, delta));
                }
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdDragSelection cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            RunOnUiThread(controller, () =>
            {
                EnsureSelection(controller, "drag");
                var targets = controller.SelectedElements.ToList();
                var delta = new Point(cmd.Dx, cmd.Dy);
                MoveSelectionWithCapturedUndo(controller, targets, delta, cmd.SnapToCentersAndEdges, "RemoteDragSelection");
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdAlignSelection cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            RunOnUiThread(controller, () =>
            {
                EnsureSelection(controller, "align");
                if (controller.SelectedElements.Count < 2)
                {
                    throw new InvalidOperationException("Select at least two shapes before attempting to align.");
                }

                var targets = controller.SelectedElements.ToList();
                var alignment = ResolveAlignment(cmd.Alignment);
                var before = CaptureRectangles(targets);
                Dictionary<GraphicElement, Rectangle> after = null;

                controller.UndoStack.UndoRedo(
                    "RemoteAlignSelection",
                    () =>
                    {
                        RestoreSelection(controller, targets);
                        controller.AlignSelected(alignment);
                        after = CaptureRectangles(targets);
                    },
                    () => RestoreRectangles(controller, before),
                    true,
                    () => RestoreRectangles(controller, after ?? before));
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdRotateSelection cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            RunOnUiThread(controller, () =>
            {
                EnsureSelection(controller, "rotate");
                var targets = controller.SelectedElements.ToList();
                var before = targets.ToDictionary(el => el, el => el.RotationAngle);
                Dictionary<GraphicElement, int> after = null;

                controller.UndoStack.UndoRedo(
                    "RemoteRotateSelection",
                    () =>
                    {
                        RestoreSelection(controller, targets);
                        controller.RotateSelected(cmd.Degrees);
                        after = targets.ToDictionary(el => el, el => el.RotationAngle);
                    },
                    () => RestoreRotations(controller, before),
                    true,
                    () => RestoreRotations(controller, after ?? before));
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdCopySelection cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);
            var editService = proc.ServiceManager.Get<IFlowSharpEditService>();

            RunOnUiThread(controller, () =>
            {
                EnsureSelection(controller, "copy");
                controller.Canvas.Focus();
                editService.Copy();
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdPasteClipboard cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);
            var editService = proc.ServiceManager.Get<IFlowSharpEditService>();

            RunOnUiThread(controller, () =>
            {
                EnsureClipboardHasFlowSharpData();
                controller.Canvas.Focus();
                editService.Paste();
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdDeleteSelection cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            RunOnUiThread(controller, () =>
            {
                EnsureSelection(controller, "delete");
                DeleteCurrentSelection(proc, controller);
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdGroupSelection cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            RunOnUiThread(controller, () =>
            {
                EnsureSelection(controller, "group");

                if (controller.SelectedElements.Any(el => el.Parent != null))
                {
                    throw new InvalidOperationException("Grouped child shapes cannot be grouped again until they are ungrouped.");
                }

                var selectedShapes = controller.SelectedElements.ToList();
                var groupBox = new GroupBoxShape(controller.Canvas);

                controller.UndoStack.UndoRedo(
                    "Group",
                    () =>
                    {
                        controller.GroupShapes(groupBox);
                        controller.DeselectCurrentSelectedElements();
                        controller.SelectElement(groupBox);
                    },
                    () =>
                    {
                        controller.UngroupShapes(groupBox, false);
                        controller.DeselectCurrentSelectedElements();
                        controller.SelectElements(selectedShapes);
                    });
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdUngroupSelection cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            RunOnUiThread(controller, () =>
            {
                if (controller.SelectedElements.Count != 1 || !(controller.SelectedElements[0] is GroupBoxShape groupBox))
                {
                    throw new InvalidOperationException("Select exactly one group box to ungroup.");
                }

                var groupedShapes = new List<GraphicElement>(groupBox.GroupChildren);
                var collapsed = groupBox.State == GroupBoxShape.CollapseState.Collapsed;
                var ungroupedState = new UngroupedGroupState(groupBox, groupedShapes, collapsed);

                controller.UndoStack.UndoRedo(
                    "Ungroup",
                    () =>
                    {
                        if (collapsed)
                        {
                            ExpandGroupBox(controller, groupBox, controller.Elements.Where(el => el.Parent == groupBox));
                        }

                        controller.UngroupShapes(groupBox, false);
                        lastUngroupedGroups[controller] = ungroupedState;
                        controller.DeselectCurrentSelectedElements();
                        controller.SelectElements(groupedShapes);
                    },
                    () =>
                    {
                        lastUngroupedGroups.Remove(controller);
                        controller.GroupShapes(groupBox);
                        controller.DeselectCurrentSelectedElements();
                        controller.SelectElement(groupBox);

                        if (collapsed)
                        {
                            CollapseGroupBox(controller, groupBox, controller.Elements.Where(el => el.Parent == groupBox));
                        }
                    });
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdRegroupSelection cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            RunOnUiThread(controller, () =>
            {
                EnsureSelection(controller, "regroup");

                if (!lastUngroupedGroups.TryGetValue(controller, out UngroupedGroupState state))
                {
                    throw new InvalidOperationException("No previous ungroup operation is available to regroup.");
                }

                GroupBoxShape groupBox = state.GroupBox;
                if (!string.IsNullOrWhiteSpace(cmd.Name) && groupBox.Name != cmd.Name)
                {
                    throw new InvalidOperationException("The last ungrouped group does not match the requested name.");
                }

                var selectedShapes = controller.SelectedElements.Where(el => el != groupBox).ToList();
                if (!selectedShapes.Any())
                {
                    throw new InvalidOperationException("Select at least one shape to regroup.");
                }

                controller.UndoStack.UndoRedo(
                    "Regroup",
                    () =>
                    {
                        controller.RegroupShapes(groupBox, selectedShapes);
                        lastUngroupedGroups.Remove(controller);
                        controller.DeselectCurrentSelectedElements();
                        controller.SelectElement(groupBox);

                        if (state.WasCollapsed)
                        {
                            CollapseGroupBox(controller, groupBox, selectedShapes);
                        }
                    },
                    () =>
                    {
                        if (state.WasCollapsed)
                        {
                            ExpandGroupBox(controller, groupBox, controller.Elements.Where(el => el.Parent == groupBox));
                        }

                        controller.UngroupShapes(groupBox, false);
                        lastUngroupedGroups[controller] = state;
                        controller.DeselectCurrentSelectedElements();
                        controller.SelectElements(selectedShapes);
                    });
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdUndo cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);
            var editService = proc.ServiceManager.Get<IFlowSharpEditService>();

            RunOnUiThread(controller, editService.Undo);
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdRedo cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);
            var editService = proc.ServiceManager.Get<IFlowSharpEditService>();

            RunOnUiThread(controller, editService.Redo);
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdConvertConnector cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            cmd.ResultJson = RunOnUiThread(controller, () =>
            {
                var connector = FindShapes(controller, cmd.Id, cmd.Name, cmd.Text, cmd.Type, true, true)
                    .OfType<Connector>()
                    .FirstOrDefault();
                if (connector == null)
                {
                    throw new InvalidOperationException("Connector was not found.");
                }

                var orientation = ResolveOrthogonalOrientation(cmd.Orientation);
                var replacement = controller.ConvertConnectorToOrthogonalWithUndo(connector, orientation);

                return JsonConvert.SerializeObject(new ConnectorCommandResult
                {
                    Count = 1,
                    ConnectorId = replacement.Id,
                    ConnectorName = replacement.Name,
                    ConnectorType = replacement.GetType().Name
                });
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdRemoveDiagonalConnectors cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            cmd.ResultJson = RunOnUiThread(controller, () =>
            {
                var previousSelection = controller.SelectedElements.ToList();
                var diagonals = controller.Elements.OfType<DiagonalConnector>().Cast<GraphicElement>().ToList();
                if (diagonals.Any())
                {
                    controller.DeselectCurrentSelectedElements();
                    controller.SelectElements(diagonals);
                    DeleteCurrentSelection(proc, controller);
                    controller.DeselectCurrentSelectedElements();
                    controller.SelectElements(previousSelection.Where(el => controller.Elements.Contains(el)).ToList());
                }

                return JsonConvert.SerializeObject(new CommandCountResult { Count = diagonals.Count });
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdSetCustomConnectionPoints cmd)
        {
            var controller = GetRequiredActiveController(proc.ServiceManager);

            cmd.ResultJson = RunOnUiThread(controller, () =>
            {
                var targets = FindShapes(controller, cmd.Id, cmd.Name, cmd.Text, cmd.Type, false, cmd.IncludeChildren)
                    .ToList();

                if (!targets.Any())
                {
                    throw new InvalidOperationException("No shapes matched the custom connection point target criteria.");
                }

                if (!cmd.All)
                {
                    targets = targets.Take(1).ToList();
                }

                var points = ParseCustomConnectionPoints(cmd.Points).ToList();
                var before = targets.ToDictionary(el => el, el => el.CustomConnectionPoints.ToList());

                controller.UndoStack.UndoRedo(
                    "RemoteCustomConnectionPoints",
                    () => targets.ForEach(el => ApplyCustomConnectionPoints(controller, el, points)),
                    () => before.ForEach(kvp => ApplyCustomConnectionPoints(controller, kvp.Key, kvp.Value)),
                    true,
                    () => targets.ForEach(el => ApplyCustomConnectionPoints(controller, el, points)));

                return JsonConvert.SerializeObject(new CommandCountResult { Count = targets.Count });
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdInspectShape cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            if (controller == null)
            {
                cmd.ShapesJson = "[]";
                return;
            }

            cmd.ShapesJson = RunOnUiThread(controller, () =>
            {
                var shapes = FindShapes(controller, cmd.Id, cmd.Name, cmd.Text, cmd.Type, cmd.IncludeConnectors, cmd.IncludeChildren)
                    .ToList();

                if (!cmd.All)
                {
                    shapes = shapes.Take(1).ToList();
                }

                var details = shapes.Select(el => BuildShapeDetail(controller, el, cmd.Properties, cmd.IncludeConnections, cmd.IncludeConnectionPoints));
                return JsonConvert.SerializeObject(details);
            });
        }

        protected ShapeSummary BuildShapeSummary(BaseController controller, GraphicElement el)
        {
            return new ShapeSummary
            {
                Id = el.Id,
                Name = el.Name,
                Text = el.Text,
                Type = el.GetType().Name,
                X = el.DisplayRectangle.X,
                Y = el.DisplayRectangle.Y,
                Width = el.DisplayRectangle.Width,
                Height = el.DisplayRectangle.Height,
                IsConnector = el.IsConnector,
                Selected = controller.SelectedElements.Contains(el),
                Visible = el.Visible,
                ParentId = el.Parent?.Id,
                ParentName = el.Parent?.Name,
                ParentType = el.Parent?.GetType().Name,
                GroupChildCount = el.GroupChildren.Count,
                ConnectionCount = el.Connections.Count,
                DistinctConnectionCount = el.Connections
                    .Select(c => new
                    {
                        ConnectorId = c.ToElement?.Id,
                        ConnectorGrip = c.ToConnectionPoint?.Type,
                        ShapeGrip = c.ElementConnectionPoint?.Type
                    })
                    .Distinct()
                    .Count()
            };
        }

        protected ShapeDetail BuildShapeDetail(BaseController controller, GraphicElement el, string propertyFilter, bool includeConnections, bool includeConnectionPoints)
        {
            var summary = BuildShapeSummary(controller, el);

            return new ShapeDetail
            {
                Id = summary.Id,
                Name = summary.Name,
                Text = summary.Text,
                Type = summary.Type,
                X = summary.X,
                Y = summary.Y,
                Width = summary.Width,
                Height = summary.Height,
                IsConnector = summary.IsConnector,
                Selected = summary.Selected,
                Visible = summary.Visible,
                ParentId = summary.ParentId,
                ParentName = summary.ParentName,
                ParentType = summary.ParentType,
                GroupChildCount = summary.GroupChildCount,
                ConnectionCount = summary.ConnectionCount,
                DistinctConnectionCount = summary.DistinctConnectionCount,
                Properties = GetInspectableProperties(el, propertyFilter),
                Connections = includeConnections ? BuildConnectionSummaries(el) : new List<ShapeConnectionSummary>(),
                ConnectionPoints = includeConnectionPoints ? BuildConnectionPointSummaries(el) : new List<ShapeConnectionPointSummary>(),
                Children = el.GroupChildren.Select(child => BuildShapeSummary(controller, child)).ToList()
            };
        }

        protected List<ShapeConnectionPointSummary> BuildConnectionPointSummaries(GraphicElement el)
        {
            var customPoints = el.GetCustomConnectionPoints()
                .Select(cp => cp.Type.ToString() + ":" + cp.Point.X.ToString(CultureInfo.InvariantCulture) + "," + cp.Point.Y.ToString(CultureInfo.InvariantCulture))
                .ToHashSet(StringComparer.Ordinal);

            return el.GetConnectionPoints().Select(cp =>
            {
                string key = cp.Type.ToString() + ":" + cp.Point.X.ToString(CultureInfo.InvariantCulture) + "," + cp.Point.Y.ToString(CultureInfo.InvariantCulture);

                return new ShapeConnectionPointSummary
                {
                    Grip = cp.Type.ToString(),
                    X = cp.Point.X,
                    Y = cp.Point.Y,
                    IsCustom = customPoints.Contains(key)
                };
            }).ToList();
        }

        protected List<ShapeConnectionSummary> BuildConnectionSummaries(GraphicElement el)
        {
            return el.Connections.Select(c =>
            {
                var connector = c.ToElement as Connector;
                var otherShape = GetOtherConnectedShape(el, connector);

                return new ShapeConnectionSummary
                {
                    ConnectorId = c.ToElement?.Id ?? Guid.Empty,
                    ConnectorName = c.ToElement?.Name,
                    ConnectorType = c.ToElement?.GetType().Name,
                    ConnectorGrip = c.ToConnectionPoint?.Type.ToString(),
                    ShapeGrip = c.ElementConnectionPoint?.Type.ToString(),
                    ConnectedShapeId = otherShape?.Id,
                    ConnectedShapeName = otherShape?.Name,
                    ConnectedShapeType = otherShape?.GetType().Name
                };
            }).ToList();
        }

        protected Dictionary<string, string> GetInspectableProperties(GraphicElement el, string propertyFilter)
        {
            var names = ParsePropertyFilter(propertyFilter);
            var properties = el.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(pi => pi.CanRead && pi.GetIndexParameters().Length == 0)
                .Where(pi => IsInspectablePropertyType(pi.PropertyType))
                .Where(pi => names == null || names.Contains(pi.Name))
                .OrderBy(pi => pi.Name);

            return properties.ToDictionary(pi => pi.Name, pi => FormatInspectableValue(pi.GetValue(el)), StringComparer.OrdinalIgnoreCase);
        }

        protected List<CanvasReference> GetCanvasReferences(Clifton.Core.ServiceManagement.IServiceManager serviceManager)
        {
            var canvasService = serviceManager.Get<IFlowSharpCanvasService>();
            var dockingService = TryGetDockingService(serviceManager);
            var references = new List<CanvasReference>();

            if (dockingService?.Documents?.Any() == true)
            {
                int index = 0;

                foreach (var doc in dockingService.Documents.Where(d => d.Metadata.LeftOf(",") == Constants.META_CANVAS))
                {
                    var controller = ResolveControllerForDocument(canvasService, doc);
                    if (controller == null)
                    {
                        continue;
                    }

                    references.Add(new CanvasReference
                    {
                        Index = index++,
                        Controller = controller,
                        Document = doc
                    });
                }

                if (references.Any())
                {
                    return references;
                }
            }

            return canvasService.Controllers
                .Select((controller, index) => new CanvasReference
                {
                    Index = index,
                    Controller = controller
                })
                .ToList();
        }

        protected CanvasSummary BuildCanvasSummary(Clifton.Core.ServiceManagement.IServiceManager serviceManager, CanvasReference canvas)
        {
            var controller = canvas.Controller;
            return new CanvasSummary
            {
                Index = canvas.Index,
                Name = GetCanvasDisplayName(canvas),
                Filename = controller.Filename,
                IsActive = controller == serviceManager.Get<IFlowSharpCanvasService>().ActiveController,
                ShapeCount = controller.Elements.Count,
                RootShapeCount = controller.Elements.Count(el => el.Parent == null),
                ConnectorCount = controller.Elements.Count(el => el.IsConnector),
                SelectedCount = controller.SelectedElements.Count
            };
        }

        protected CanvasViewSummary BuildCanvasViewSummary(BaseController controller)
        {
            return new CanvasViewSummary
            {
                Zoom = controller.Zoom,
                OffsetX = controller.CanvasOffset.X,
                OffsetY = controller.CanvasOffset.Y,
                ViewportOriginX = controller.Canvas.ViewportOrigin.X,
                ViewportOriginY = controller.Canvas.ViewportOrigin.Y
            };
        }

        protected CanvasReference ResolveCanvasReference(Clifton.Core.ServiceManagement.IServiceManager serviceManager, CmdUseCanvas cmd)
        {
            var canvases = GetCanvasReferences(serviceManager);
            if (cmd.Index.HasValue)
            {
                return canvases.SingleOrDefault(c => c.Index == cmd.Index.Value);
            }

            if (!string.IsNullOrWhiteSpace(cmd.Name))
            {
                return canvases.SingleOrDefault(c =>
                    string.Equals(GetCanvasDisplayName(c), cmd.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Document?.TabText, cmd.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Controller.CanvasName, cmd.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(cmd.Filename))
            {
                return canvases.SingleOrDefault(c => PathsEqual(c.Controller.Filename, cmd.Filename));
            }

            throw new InvalidOperationException("Specify Index, Name, or Filename.");
        }

        protected void ActivateCanvas(Clifton.Core.ServiceManagement.IServiceManager serviceManager, CanvasReference canvas)
        {
            var canvasService = serviceManager.Get<IFlowSharpCanvasService>();
            var dockingService = TryGetDockingService(serviceManager);

            if (canvas.Document != null && dockingService != null)
            {
                dockingService.SetActiveDocument(canvas.Document);
            }
            else if (canvas.Controller.Canvas.Parent != null)
            {
                canvasService.SetActiveController(canvas.Controller.Canvas.Parent);
            }

            canvas.Controller.Canvas.Focus();
        }

        protected string GetCanvasDisplayName(CanvasReference canvas)
        {
            return !string.IsNullOrWhiteSpace(canvas.Controller.CanvasName) ? canvas.Controller.CanvasName :
                !string.IsNullOrWhiteSpace(canvas.Document?.TabText) ? canvas.Document.TabText :
                !string.IsNullOrWhiteSpace(canvas.Controller.Filename) ? Path.GetFileNameWithoutExtension(canvas.Controller.Filename) :
                "Canvas " + canvas.Index.ToString(CultureInfo.InvariantCulture);
        }

        protected BaseController ResolveControllerForDocument(IFlowSharpCanvasService canvasService, IDockDocument document)
        {
            if (document is Control control && control.Controls.Count > 0)
            {
                var parent = control.Controls[0];
                return canvasService.Controllers.FirstOrDefault(c => ReferenceEquals(c.Canvas.Parent, parent));
            }

            return null;
        }

        protected IDockingFormService TryGetDockingService(Clifton.Core.ServiceManagement.IServiceManager serviceManager)
        {
            try
            {
                return serviceManager.Get<IDockingFormService>();
            }
            catch
            {
                return null;
            }
        }

        protected void ApplySelection(BaseController controller, IEnumerable<GraphicElement> matches, SelectionMode mode)
        {
            var targets = matches.Distinct().ToList();

            switch (mode)
            {
                case SelectionMode.Replace:
                    controller.DeselectCurrentSelectedElements();
                    targets.ForEach(controller.SelectElement);
                    break;

                case SelectionMode.Add:
                    targets.ForEach(controller.SelectElement);
                    break;

                case SelectionMode.Remove:
                    targets.Where(el => controller.SelectedElements.Contains(el)).ToList().ForEach(controller.DeselectElement);
                    break;

                default:
                    throw new InvalidOperationException("Unknown selection mode.");
            }
        }

        protected SelectionMode ResolveSelectionMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                return SelectionMode.Replace;
            }

            switch (mode.Trim().ToLowerInvariant())
            {
                case "replace":
                case "set":
                    return SelectionMode.Replace;

                case "add":
                case "append":
                    return SelectionMode.Add;

                case "remove":
                case "subtract":
                    return SelectionMode.Remove;

                default:
                    throw new InvalidOperationException("Unsupported selection mode '" + mode + "'.");
            }
        }

        protected Rectangle NormalizeRectangle(int x, int y, int width, int height)
        {
            var left = width >= 0 ? x : x + width;
            var top = height >= 0 ? y : y + height;
            return new Rectangle(left, top, Math.Abs(width), Math.Abs(height));
        }

        protected void MoveCapturedSelection(BaseController controller, List<GraphicElement> targets, Point delta, bool snapToCentersAndEdges = false)
        {
            if (!HaveSameSelection(controller, targets))
            {
                controller.DeselectCurrentSelectedElements();
                controller.SelectElements(targets);
            }

            controller.MoveSelectedElements(delta, snapToCentersAndEdges);
        }

        protected void MoveSelectionWithCapturedUndo(BaseController controller, List<GraphicElement> targets, Point delta, bool snapToCentersAndEdges, string commandName)
        {
            var before = CaptureRectangles(targets);
            Dictionary<GraphicElement, Rectangle> after = null;

            controller.UndoStack.UndoRedo(
                commandName,
                () =>
                {
                    MoveCapturedSelection(controller, targets, delta, snapToCentersAndEdges);
                    after = CaptureRectangles(targets);
                },
                () => RestoreRectangles(controller, before),
                true,
                () => RestoreRectangles(controller, after ?? before));
        }

        protected bool HaveSameSelection(BaseController controller, List<GraphicElement> targets)
        {
            return controller.SelectedElements.Count == targets.Count && controller.SelectedElements.All(targets.Contains);
        }

        protected Dictionary<GraphicElement, Rectangle> CaptureRectangles(IEnumerable<GraphicElement> targets)
        {
            return targets.ToDictionary(el => el, el => el.DisplayRectangle);
        }

        protected void RestoreSelection(BaseController controller, List<GraphicElement> targets)
        {
            if (HaveSameSelection(controller, targets))
            {
                return;
            }

            controller.DeselectCurrentSelectedElements();
            controller.SelectElements(targets);
        }

        protected void RestoreRectangles(BaseController controller, Dictionary<GraphicElement, Rectangle> rectangles)
        {
            rectangles.ForEach(kvp =>
            {
                controller.Redraw(kvp.Key, el =>
                {
                    el.DisplayRectangle = kvp.Value;
                    el.UpdatePath();
                    controller.UpdateConnections(el);
                });
            });
        }

        protected void RestoreRotations(BaseController controller, Dictionary<GraphicElement, int> rotations)
        {
            rotations.ForEach(kvp =>
            {
                kvp.Key.RotationAngle = kvp.Value;
                controller.Redraw(kvp.Key);
            });
        }

        protected void EnsureSelection(BaseController controller, string action)
        {
            if (!controller.SelectedElements.Any())
            {
                throw new InvalidOperationException("Select one or more shapes before attempting to " + action + ".");
            }
        }

        protected void EnsureClipboardHasFlowSharpData()
        {
            if (!Clipboard.ContainsData("FlowSharp"))
            {
                throw new InvalidOperationException("Clipboard does not contain FlowSharp data.");
            }
        }

        protected void DeleteCurrentSelection(ISemanticProcessor proc, BaseController controller)
        {
            var originalZOrder = controller.GetZOrderOfSelectedElements();
            var selectedElements = controller.SelectedElements.ToList();
            var elementParents = selectedElements.ToDictionary(el => el, el => el.Parent);
            var mouseController = proc.ServiceManager.Get<IFlowSharpMouseControllerService>();

            selectedElements.ForEach(mouseController.ShapeDeleted);

            controller.UndoStack.UndoRedo(
                "Delete",
                () =>
                {
                    controller.DeleteSelectedElementsHierarchy(false);
                    selectedElements.Where(el => elementParents[el] != null).ToList().ForEach(el =>
                    {
                        elementParents[el].GroupChildren.Remove(el);
                        el.Parent = null;
                    });
                },
                () =>
                {
                    RestoreDeletedSelection(controller, originalZOrder, selectedElements, elementParents);
                });
        }

        protected void RestoreDeletedSelection(BaseController controller, List<ZOrderMap> originalZOrder, List<GraphicElement> selectedElements, Dictionary<GraphicElement, GraphicElement> elementParents)
        {
            controller.RestoreZOrderWithHierarchy(originalZOrder);
            RestoreDeletedConnections(originalZOrder);
            controller.DeselectCurrentSelectedElements();
            controller.SelectElements(selectedElements);

            selectedElements.ForEach(el =>
            {
                el.Parent = elementParents[el];
                el.Parent?.GroupChildren.AddIfUnique(el);
            });
        }

        protected void RestoreDeletedConnections(List<ZOrderMap> zorder)
        {
            foreach (var zom in zorder)
            {
                var el = zom.Element;

                foreach (var conn in zom.Connections)
                {
                    conn.ToElement.SetConnection(conn.ToConnectionPoint.Type, el);
                }

                if (!(el is Connector connector))
                {
                    continue;
                }

                connector.StartConnectedShape = zom.StartConnectedShape;
                connector.EndConnectedShape = zom.EndConnectedShape;

                if (connector.StartConnectedShape != null && zom.StartConnection != null)
                {
                    connector.StartConnectedShape.SetConnection(GripType.Start, connector);
                    connector.StartConnectedShape.AddConnection(zom.StartConnection);
                }

                if (connector.EndConnectedShape != null && zom.EndConnection != null)
                {
                    connector.EndConnectedShape.SetConnection(GripType.End, connector);
                    connector.EndConnectedShape.AddConnection(zom.EndConnection);
                }
            }
        }

        protected void CollapseGroupBox(BaseController canvasController, GroupBoxShape groupBox, IEnumerable<GraphicElement> children)
        {
            canvasController.Redraw(groupBox, _ =>
            {
                groupBox.SetCollapsedState();
                groupBox.SaveExpandedSize();
                canvasController.Elements.Where(el => el.Parent == groupBox).ForEach(el => el.Visible = false);
                var rect = groupBox.DisplayRectangle;
                groupBox.DisplayRectangle = new Rectangle(rect.Location, new Size(rect.Width, 30));
                canvasController.UpdateConnections(groupBox);
            });
        }

        protected void ExpandGroupBox(BaseController canvasController, GroupBoxShape groupBox, IEnumerable<GraphicElement> children)
        {
            canvasController.Redraw(groupBox, _ =>
            {
                groupBox.SetExpandedState();
                var rect = groupBox.DisplayRectangle;
                groupBox.DisplayRectangle = new Rectangle(rect.Location, groupBox.ExpandedSize);
                canvasController.UpdateConnections(groupBox);
            });

            children.ForEach(el =>
            {
                el.Visible = true;
                el.Redraw();
            });
        }

        protected GraphicElement GetOtherConnectedShape(GraphicElement source, Connector connector)
        {
            if (connector == null)
            {
                return null;
            }

            if (connector.StartConnectedShape == source)
            {
                return connector.EndConnectedShape;
            }

            if (connector.EndConnectedShape == source)
            {
                return connector.StartConnectedShape;
            }

            return connector.StartConnectedShape ?? connector.EndConnectedShape;
        }

        protected HashSet<string> ParsePropertyFilter(string propertyFilter)
        {
            if (string.IsNullOrWhiteSpace(propertyFilter))
            {
                return null;
            }

            return propertyFilter
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        protected bool IsInspectablePropertyType(Type type)
        {
            var convertedType = Nullable.GetUnderlyingType(type) ?? type;

            return convertedType.IsPrimitive ||
                convertedType.IsEnum ||
                convertedType == typeof(string) ||
                convertedType == typeof(decimal) ||
                convertedType == typeof(Guid) ||
                convertedType == typeof(Color) ||
                convertedType == typeof(Point) ||
                convertedType == typeof(Size) ||
                convertedType == typeof(Rectangle) ||
                convertedType == typeof(Font);
        }

        protected string FormatInspectableValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            switch (value)
            {
                case Color color:
                    return color.A == 255 ?
                        $"#{color.R:X2}{color.G:X2}{color.B:X2}" :
                        $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

                case Point point:
                    return point.X.ToString(CultureInfo.InvariantCulture) + "," + point.Y.ToString(CultureInfo.InvariantCulture);

                case Size size:
                    return size.Width.ToString(CultureInfo.InvariantCulture) + "," + size.Height.ToString(CultureInfo.InvariantCulture);

                case Rectangle rect:
                    return string.Join(",",
                        rect.X.ToString(CultureInfo.InvariantCulture),
                        rect.Y.ToString(CultureInfo.InvariantCulture),
                        rect.Width.ToString(CultureInfo.InvariantCulture),
                        rect.Height.ToString(CultureInfo.InvariantCulture));

                case Font font:
                    return string.Join(",",
                        font.FontFamily.Name,
                        font.Size.ToString(CultureInfo.InvariantCulture),
                        font.Style.ToString());

                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        protected BaseController GetRequiredActiveController(Clifton.Core.ServiceManagement.IServiceManager serviceManager)
        {
            return serviceManager.Get<IFlowSharpCanvasService>().ActiveController ??
                throw new InvalidOperationException("No active canvas is available.");
        }

        protected bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            var resolvedLeft = ResolvePath(left);
            var resolvedRight = ResolvePath(right);
            return string.Equals(resolvedLeft, resolvedRight, StringComparison.OrdinalIgnoreCase);
        }

        protected Point Reverse(Point point)
        {
            return new Point(-point.X, -point.Y);
        }

        protected RuntimePropertyChange CreatePropertyChange(GraphicElement element, string propertyName, string value)
        {
            var properties = element.CreateProperties();
            var property = TryResolveProperty(properties, propertyName);
            if (property != null)
            {
                var newValue = ConvertRuntimeValue(property.PropertyType, value);

                return new RuntimePropertyChange
                {
                    Element = element,
                    PropertyName = property.Name,
                    OldValue = property.GetValue(properties),
                    NewValue = newValue,
                    RedrawMode = properties.GetRedrawMode(property.Name)
                };
            }

            var elementProperty = ResolveElementProperty(element, propertyName);
            var elementNewValue = ConvertRuntimeValue(elementProperty.PropertyType, value);

            return new RuntimePropertyChange
            {
                Element = element,
                PropertyName = elementProperty.Name,
                OldValue = elementProperty.GetValue(element),
                NewValue = elementNewValue,
                RedrawMode = PropertyRedrawMode.Element,
                UseElementProperty = true
            };
        }

        protected PropertyInfo TryResolveProperty(ElementProperties properties, string propertyName)
        {
            var property = properties.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            return property?.CanWrite == true ? property : null;
        }

        protected PropertyInfo ResolveProperty(ElementProperties properties, string propertyName)
        {
            var property = TryResolveProperty(properties, propertyName);

            if (property == null)
            {
                throw new InvalidOperationException("Property '" + propertyName + "' is not editable for this shape.");
            }

            return property;
        }

        protected PropertyInfo ResolveElementProperty(GraphicElement element, string propertyName)
        {
            var property = element.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property == null || !property.CanWrite || property.GetIndexParameters().Length != 0 || !IsInspectablePropertyType(property.PropertyType))
            {
                throw new InvalidOperationException("Property '" + propertyName + "' is not editable for this shape.");
            }

            return property;
        }

        protected object ConvertRuntimeValue(Type targetType, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                {
                    return null;
                }

                return Activator.CreateInstance(targetType);
            }

            var convertedType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (convertedType.IsEnum)
            {
                return Enum.Parse(convertedType, value, true);
            }

            if (convertedType == typeof(Color))
            {
                return GetColor(value);
            }

            var converter = TypeDescriptor.GetConverter(convertedType);
            if (converter != null && converter.CanConvertFrom(typeof(string)))
            {
                return converter.ConvertFromInvariantString(value);
            }

            return Convert.ChangeType(value, convertedType, CultureInfo.InvariantCulture);
        }

        protected void ApplyRuntimePropertyChange(
            BaseController controller,
            GraphicElement element,
            string propertyName,
            object value,
            PropertyRedrawMode redrawMode,
            bool useElementProperty)
        {
            if (useElementProperty)
            {
                var elementProperty = ResolveElementProperty(element, propertyName);
                controller.Redraw(element, el => ApplyRuntimeElementPropertyValue(controller, el, elementProperty, value));
                return;
            }

            var properties = element.CreateProperties();
            var property = ResolveProperty(properties, propertyName);

            if (redrawMode == PropertyRedrawMode.None)
            {
                ApplyRuntimePropertyValue(controller, element, properties, property, property.Name, value, redrawMode);
                return;
            }

            controller.Redraw(element, el =>
                ApplyRuntimePropertyValue(controller, el, properties, property, property.Name, value, redrawMode));
        }

        protected void ApplyRuntimeElementPropertyValue(
            BaseController controller,
            GraphicElement element,
            PropertyInfo property,
            object value)
        {
            property.SetValue(element, value);
            element.UpdateProperties();
            element.UpdatePath();

            if (property.Name == nameof(GraphicElement.DisplayRectangle) ||
                property.Name == nameof(GraphicElement.RotationAngle))
            {
                controller.UpdateConnections(element);
            }
        }

        protected void ApplyRuntimePropertyValue(
            BaseController controller,
            GraphicElement element,
            ElementProperties properties,
            PropertyInfo property,
            string propertyName,
            object value,
            PropertyRedrawMode redrawMode)
        {
            property.SetValue(properties, value);
            properties.Update(element, propertyName);
            element.UpdateProperties();
            element.UpdatePath();

            if (redrawMode == PropertyRedrawMode.ElementAndConnections)
            {
                controller.UpdateConnections(element);
            }
        }

        protected GripType ResolveAlignment(string alignment)
        {
            switch ((alignment ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "left":
                case "lefts":
                case "leftmiddle":
                    return GripType.LeftMiddle;

                case "right":
                case "rights":
                case "rightmiddle":
                    return GripType.RightMiddle;

                case "top":
                case "tops":
                case "topmiddle":
                    return GripType.TopMiddle;

                case "bottom":
                case "bottoms":
                case "bottommiddle":
                    return GripType.BottomMiddle;

                default:
                    throw new InvalidOperationException("Unsupported alignment '" + alignment + "'.");
            }
        }

        protected OrthogonalConnectorOrientation ResolveOrthogonalOrientation(string orientation)
        {
            switch ((orientation ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "leftright":
                case "left-right":
                case "lr":
                case "horizontal":
                    return OrthogonalConnectorOrientation.LeftRight;

                case "updown":
                case "up-down":
                case "ud":
                case "vertical":
                    return OrthogonalConnectorOrientation.UpDown;

                default:
                    throw new InvalidOperationException("Unsupported connector orientation '" + orientation + "'.");
            }
        }

        protected IEnumerable<ConnectionPoint> ParseCustomConnectionPoints(string points)
        {
            if (string.IsNullOrWhiteSpace(points))
            {
                return Enumerable.Empty<ConnectionPoint>();
            }

            return points
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseCustomConnectionPoint)
                .ToList();
        }

        protected ConnectionPoint ParseCustomConnectionPoint(string value)
        {
            var separator = value.IndexOf(':');
            if (separator <= 0)
            {
                throw new InvalidOperationException("Custom connection points must use Grip:X,Y format.");
            }

            var gripText = value.Substring(0, separator);
            var coordinateText = value.Substring(separator + 1);
            var coordinates = coordinateText.Split(',');
            if (coordinates.Length != 2)
            {
                throw new InvalidOperationException("Custom connection point coordinates must use X,Y format.");
            }

            if (!Enum.TryParse<GripType>(gripText, true, out var grip))
            {
                throw new InvalidOperationException("Unsupported connection point grip '" + gripText + "'.");
            }

            int x = int.Parse(coordinates[0], NumberStyles.Integer, CultureInfo.InvariantCulture);
            int y = int.Parse(coordinates[1], NumberStyles.Integer, CultureInfo.InvariantCulture);
            return new ConnectionPoint(grip, new Point(x, y));
        }

        protected void ApplyCustomConnectionPoints(BaseController controller, GraphicElement element, IEnumerable<ConnectionPoint> points)
        {
            element.SetCustomConnectionPoints(points.Select(point => new ConnectionPoint(point.Type, point.Point)).ToList());
            controller.Redraw(element, el =>
            {
                el.UpdatePath();
                controller.UpdateConnections(el);
            });
        }

        protected sealed class RuntimePropertyChange
        {
            public GraphicElement Element { get; set; }
            public string PropertyName { get; set; }
            public object OldValue { get; set; }
            public object NewValue { get; set; }
            public PropertyRedrawMode RedrawMode { get; set; }
            public bool UseElementProperty { get; set; }
        }

        private sealed class UngroupedGroupState
        {
            public UngroupedGroupState(GroupBoxShape groupBox, List<GraphicElement> groupedShapes, bool wasCollapsed)
            {
                GroupBox = groupBox;
                GroupedShapes = groupedShapes;
                WasCollapsed = wasCollapsed;
            }

            public GroupBoxShape GroupBox { get; }
            public List<GraphicElement> GroupedShapes { get; }
            public bool WasCollapsed { get; }
        }

        protected sealed class CanvasReference
        {
            public int Index { get; set; }
            public BaseController Controller { get; set; }
            public IDockDocument Document { get; set; }
        }

        protected enum SelectionMode
        {
            Replace,
            Add,
            Remove
        }
    }
}
