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
using System.Text;
using System.Threading;
using System.Windows.Forms;

using Newtonsoft.Json;

using Clifton.Core.ExtensionMethods;
using Clifton.Core.Utils;
using Clifton.Core.Semantics;
using Clifton.Core.ServiceManagement;

using FlowSharpCodeServiceInterfaces;
using FlowSharpCodeShapeInterfaces;
using FlowSharpLib;
using FlowSharpServiceInterfaces;

namespace FlowSharpRestService
{
    public partial class CommandProcessor : IReceptor
    {
        // Ex: localhost:8001/flowsharp?cmd=CmdUpdateProperty&Name=btnTest&PropertyName=Text&Value=Foobar
        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdUpdateProperty cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            if (controller == null) return;

            RunOnUiThread(controller, () =>
            {
                var els = controller.Elements.Where(e => e.Name == cmd.Name).ToList();
                els.ForEach(el =>
                {
                    var pi = el.GetType().GetProperty(cmd.PropertyName);
                    if (pi == null) return;
                    var cval = Converter.Convert(cmd.Value, pi.PropertyType);
                    pi.SetValue(el, cval);
                    controller.Redraw(el);
                });
            });
        }

        // Ex: localhost:8001/flowsharp?cmd=CmdShowShape&Name=btnTest
        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdShowShape cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            if (controller == null) return;

            RunOnUiThread(controller, () =>
            {
                var el = FindShapes(controller, cmd.Id, cmd.Name, cmd.Text, null, true).FirstOrDefault();
                if (el == null) return;
                controller.FocusOn(el);
            });
        }

        // Ex: localhost:8001/flowsharp?cmd=CmdGetShapeFiles
        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdGetShapeFiles cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            if (controller == null) return;

            RunOnUiThread(controller, () =>
            {
                var els = controller.Elements.Where(e => e is IFileBox);
                cmd.Filenames.AddRange(els.Cast<IFileBox>().Where(el => !string.IsNullOrEmpty(el.Filename)).Select(el => el.Filename));
            });
        }

        // FlowSharpCodeOutputWindowService required for this behavior.
        // Ex: localhost:8001/flowsharp?cmd=CmdOutputMessage&Text=foobar
        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdOutputMessage cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            if (controller == null) return;

            var w = proc.ServiceManager.Get<IFlowSharpCodeOutputWindowService>();
            var text = cmd.Text ?? string.Empty;

            RunOnUiThread(controller, () =>
            {
                text.Split('\n').Where(s => !string.IsNullOrEmpty(s.Trim())).ForEach(s => w.WriteLine(s.Trim()));
            });
        }

        // Ex: localhost:8001/flowsharp?cmd=CmdDropShape&ShapeName=Box&X=50&Y=100
        // Ex: localhost:8001/flowsharp?cmd=CmdDropShape&ShapeName=Box&X=50&Y=100&Text=Foobar&FillColor=!FF00ff&Width=300
        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdDropShape cmd)
        {
            var shapes = proc.ServiceManager.Get<IFlowSharpToolboxService>().ShapeList;
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            if (controller == null) return;

            var t = shapes.SingleOrDefault(s => s.Name == cmd.ShapeName && typeof(GraphicElement).IsAssignableFrom(s) && !typeof(Connector).IsAssignableFrom(s));
            if (t == null)
            {
                throw new InvalidOperationException("Unknown shape '" + cmd.ShapeName + "'.");
            }

            RunOnUiThread(controller, () =>
            {
                var el = (GraphicElement)Activator.CreateInstance(t, new object[] { controller.Canvas });
                var defaultRect = el.DefaultRectangle();
                var width = cmd.Width ?? defaultRect.Width;
                var height = cmd.Height ?? defaultRect.Height;
                el.DisplayRectangle = new Rectangle(cmd.X, cmd.Y, ClampShapeWidth(width), ClampShapeHeight(height));
                el.Name = cmd.Name;
                el.Text = cmd.Text;

                cmd.FillColor.IfNotNull(c => el.FillColor = GetColor(c));
                cmd.BorderColor.IfNotNull(c => el.BorderPenColor = GetColor(c));
                cmd.TextColor.IfNotNull(c => el.TextColor = GetColor(c));

                el.UpdateProperties();
                el.UpdatePath();
                controller.Insert(el);

                if (cmd.AutoGroup && !el.IsConnector)
                {
                    var targetGroup = controller.GetContainingGroupBox(el);
                    controller.AddShapeToGroup(targetGroup, el);
                }
            });
        }

        // Ex: localhost:8001/flowsharp?cmd=CmdDropConnector&ConnectorName=DiagonalConnector&X1=50&Y1=100&X2=150&Y2=150
        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdDropConnector cmd)
        {
            var shapes = proc.ServiceManager.Get<IFlowSharpToolboxService>().ShapeList;
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            if (controller == null) return;

            var t = shapes.SingleOrDefault(s => s.Name == cmd.ConnectorName && typeof(Connector).IsAssignableFrom(s));
            if (t == null)
            {
                throw new InvalidOperationException("Unknown connector '" + cmd.ConnectorName + "'.");
            }

            RunOnUiThread(controller, () =>
            {
                var el = (Connector)Activator.CreateInstance(t, new object[] { controller.Canvas });
                el.Name = cmd.Name;
                cmd.BorderColor.IfNotNull(c => el.BorderPenColor = GetColor(c));
                SetConnectorEndpoints(el, new Point(cmd.X1, cmd.Y1), new Point(cmd.X2, cmd.Y2));
                el.UpdatePath();
                controller.Insert(el);
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdClearCanvas cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            if (controller == null) return;

            RunOnUiThread(controller, () =>
            {
                controller.Clear();
                controller.Canvas.Invalidate();
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdLoadDiagram cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd.Filename)) return;

            var canvasService = proc.ServiceManager.Get<IFlowSharpCanvasService>();
            var filename = ResolvePath(cmd.Filename);
            if (!File.Exists(filename))
            {
                throw new FileNotFoundException("Diagram file was not found.", filename);
            }

            RunOnUiThread(proc.ServiceManager, () =>
            {
                canvasService.LoadDiagrams(filename);
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdDeleteShape cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            if (controller == null) return;

            RunOnUiThread(controller, () =>
            {
                var targets = FindShapes(controller, cmd.Id, cmd.Name, cmd.Text, null, true).ToList();
                if (!targets.Any()) return;

                if (!cmd.All)
                {
                    targets = targets.Take(1).ToList();
                }

                targets.ForEach(el => DeleteShapeHierarchy(controller, el));
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdMoveShape cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            if (controller == null) return;

            RunOnUiThread(controller, () =>
            {
                var targets = FindShapes(controller, cmd.Id, cmd.Name, cmd.Text, null, true).ToList();
                if (!targets.Any()) return;

                targets.ForEach(shape =>
                {
                    var current = shape.DisplayRectangle;
                    bool hasX = cmd.X.HasValue;
                    bool hasY = cmd.Y.HasValue;
                    bool hasDx = cmd.Dx.HasValue;
                    bool hasDy = cmd.Dy.HasValue;
                    bool hasWidth = cmd.Width.HasValue;
                    bool hasHeight = cmd.Height.HasValue;

                    int newX = current.X;
                    int newY = current.Y;
                    bool moved = false;

                    if (hasDx || hasDy)
                    {
                        var dx = cmd.Dx ?? 0;
                        var dy = cmd.Dy ?? 0;
                        controller.MoveElement(shape, new Point(dx, dy));
                        moved = true;
                        current = shape.DisplayRectangle;
                    }
                    else if (hasX || hasY)
                    {
                        if (!cmd.Relative)
                        {
                            newX = cmd.X ?? current.X;
                            newY = cmd.Y ?? current.Y;
                            controller.MoveElementTo(shape, new Point(newX, newY));
                            moved = true;
                            current = shape.DisplayRectangle;
                        }
                        else
                        {
                            newX = current.X + (cmd.X ?? 0);
                            newY = current.Y + (cmd.Y ?? 0);
                            controller.MoveElement(shape, new Point(cmd.X ?? 0, cmd.Y ?? 0));
                            moved = true;
                            current = shape.DisplayRectangle;
                        }
                    }

                    if (hasWidth || hasHeight)
                    {
                        var newWidth = ClampShapeWidth(cmd.Width ?? current.Width);
                        var newHeight = ClampShapeHeight(cmd.Height ?? current.Height);
                        if (!moved)
                        {
                            newX = current.X;
                            newY = current.Y;
                        }

                        shape.DisplayRectangle = new Rectangle(newX, newY, newWidth, newHeight);
                        shape.UpdatePath();
                        controller.UpdateConnections(shape);
                        controller.Redraw(shape);
                    }
                    else if (moved)
                    {
                        controller.UpdateConnections(shape);
                    }
                });
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdConnectShapes cmd)
        {
            var shapes = proc.ServiceManager.Get<IFlowSharpToolboxService>().ShapeList;
            var canvasService = proc.ServiceManager.Get<IFlowSharpCanvasService>();
            var controller = canvasService.ActiveController;
            if (controller == null) return;

            var connectorName = cmd.ConnectorName ?? "DiagonalConnector";
            var type = shapes.SingleOrDefault(s => s.Name == connectorName && typeof(Connector).IsAssignableFrom(s));
            if (type == null)
            {
                throw new InvalidOperationException("Unknown connector '" + connectorName + "'.");
            }

            RunOnUiThread(controller, () =>
            {
                var source = FindShapes(controller, cmd.SourceId, cmd.SourceName ?? cmd.Source, cmd.SourceText, null, false).FirstOrDefault();
                if (source == null)
                {
                    throw new InvalidOperationException("Source shape was not found.");
                }

                var target = FindShapes(controller, cmd.TargetId, cmd.TargetName ?? cmd.Target, cmd.TargetText, null, false).FirstOrDefault();
                if (target == null)
                {
                    throw new InvalidOperationException("Target shape was not found.");
                }

                var sourceConnectionPoints = source.GetConnectionPoints();
                if (!sourceConnectionPoints.Any())
                {
                    throw new InvalidOperationException("Source shape has no connection points.");
                }

                var targetConnectionPoints = target.GetConnectionPoints();
                if (!targetConnectionPoints.Any())
                {
                    throw new InvalidOperationException("Target shape has no connection points.");
                }

                var sourceGrip = ResolveConnectionGrip(sourceConnectionPoints, cmd.SourceGrip, GripType.RightMiddle);
                var targetGrip = ResolveConnectionGrip(targetConnectionPoints, cmd.TargetGrip, GripType.LeftMiddle);
                var sourceLineGrip = ResolveConnectorGrip(cmd.SourceGrip, GripType.Start);
                var targetLineGrip = ResolveConnectorGrip(cmd.TargetGrip, GripType.End);
                var sourceCp = sourceConnectionPoints.First(cp => cp.Type == sourceGrip);
                var targetCp = targetConnectionPoints.First(cp => cp.Type == targetGrip);

                var connector = (Connector)Activator.CreateInstance(type, new object[] { controller.Canvas });
                SetConnectorEndpoints(connector, sourceCp.Point, targetCp.Point);
                if (TryParseCap(cmd.StartCap, out var startCap)) connector.StartCap = startCap;
                if (TryParseCap(cmd.EndCap, out var endCap)) connector.EndCap = endCap;
                connector.UpdateProperties();
                connector.UpdatePath();
                connector.Name = cmd.Name;
                source.AddConnection(new Connection { ToElement = connector, ToConnectionPoint = new ConnectionPoint(sourceLineGrip, sourceCp.Point), ElementConnectionPoint = sourceCp });
                target.AddConnection(new Connection { ToElement = connector, ToConnectionPoint = new ConnectionPoint(targetLineGrip, targetCp.Point), ElementConnectionPoint = targetCp });
                connector.SetConnection(sourceLineGrip, source);
                connector.SetConnection(targetLineGrip, target);
                controller.Insert(connector);
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdListShapes cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            if (controller == null)
            {
                cmd.ShapesJson = "[]";
                return;
            }

            cmd.ShapesJson = RunOnUiThread(controller, () =>
            {
                var shapes = FindShapes(controller, cmd.Id, cmd.Name, cmd.Text, cmd.Type, cmd.IncludeConnectors, cmd.IncludeChildren, cmd.SelectedOnly).ToList();
                return JsonConvert.SerializeObject(shapes.Select(el => BuildShapeSummary(controller, el)));
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdRunMacro cmd)
        {
            int macroStepDelayMilliseconds = RuntimeControlConfiguration.GetMacroStepDelayMilliseconds();
            string script = null;
            if (!string.IsNullOrWhiteSpace(cmd.Filename))
            {
                var path = ResolvePath(cmd.Filename);
                if (!File.Exists(path))
                {
                    cmd.ResultJson = JsonConvert.SerializeObject(new[]
                    {
                        new MacroResult { Step = 0, Command = "loadmacro", Success = false, Error = "Macro file was not found: " + path }
                    });
                    return;
                }

                script = File.ReadAllText(path);
            }

            if (script == null)
            {
                script = cmd.Script;
            }

            if (script == null)
            {
                script = string.Empty;
            }
            if (string.IsNullOrWhiteSpace(script))
            {
                cmd.ResultJson = JsonConvert.SerializeObject(new[]
                {
                    new MacroResult { Step = 0, Command = "runmacro", Success = false, Error = "No script provided." }
                });
                return;
            }

            var results = new List<MacroResult>();
            var lines = script.Replace("\r\n", "\n").Split('\n');
            int step = 0;
            bool hasExecutedCommand = false;

            foreach (var rawLine in lines)
            {
                step++;
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal)) continue;

                var result = new MacroResult { Step = step, Command = line };
                var parsed = ParseMacroLine(line, out var cmdName, out var values);
                if (!parsed)
                {
                    result.Success = false;
                    result.Error = "Invalid macro syntax";
                    results.Add(result);
                    if (!cmd.ContinueOnError) break;
                    continue;
                }

                if (hasExecutedCommand && macroStepDelayMilliseconds > 0)
                {
                    Thread.Sleep(macroStepDelayMilliseconds);
                }

                try
                {
                    var command = SemanticTypeParser.NewCommand(cmdName);
                    if (command == null)
                    {
                        result.Success = false;
                        result.Error = "Unknown command '" + cmdName + "'";
                        results.Add(result);
                        if (!cmd.ContinueOnError) break;
                        continue;
                    }

                    SemanticTypeParser.PopulateType(command, values);
                    var commandResult = ExecuteCommand(proc, membrane, command);
                    result.Success = commandResult.Success;
                    result.Error = commandResult.Error;
                    result.Response = commandResult.Response;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                results.Add(result);
                hasExecutedCommand = true;
                if (!result.Success && !cmd.ContinueOnError)
                {
                    break;
                }
            }

            cmd.ResultJson = JsonConvert.SerializeObject(results);
        }

        private bool ParseMacroLine(string line, out string cmd, out Dictionary<string, string> values)
        {
            cmd = null;
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var tokens = new List<string>();
            var buffer = new StringBuilder();
            bool quoted = false;
            char quoteChar = '\0';

            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (ch == '\\' && quoted && i + 1 < line.Length)
                {
                    buffer.Append(line[i + 1]);
                    i++;
                    continue;
                }

                if (!quoted && ch == '#' )
                {
                    break;
                }

                if (quoted)
                {
                    if (ch == quoteChar)
                    {
                        quoted = false;
                        quoteChar = '\0';
                    }
                    else
                    {
                        buffer.Append(ch);
                    }

                    continue;
                }

                if (ch == '"' || ch == '\'')
                {
                    quoted = true;
                    quoteChar = ch;
                    continue;
                }

                if (char.IsWhiteSpace(ch))
                {
                    if (buffer.Length > 0)
                    {
                        tokens.Add(buffer.ToString());
                        buffer.Clear();
                    }
                }
                else
                {
                    buffer.Append(ch);
                }
            }

            if (buffer.Length > 0)
            {
                tokens.Add(buffer.ToString());
            }

            if (!tokens.Any())
            {
                return false;
            }

            var first = tokens.First();
            var eq = first.IndexOf('=');
            if (eq > 0)
            {
                var key = first.Substring(0, eq);
                var value = first.Substring(eq + 1);
                if (key.Equals("cmd", StringComparison.OrdinalIgnoreCase))
                {
                    cmd = value;
                }
                else
                {
                    cmd = key;
                    values[key] = value;
                }
            }
            else
            {
                cmd = first;
            }

            cmd = SemanticTypeParser.NormalizeCommandName(cmd);
            if (string.IsNullOrWhiteSpace(cmd))
            {
                return false;
            }

            foreach (var token in tokens.Skip(1))
            {
                var key = token;
                var val = string.Empty;
                var tokenEquals = token.IndexOf('=');
                if (tokenEquals >= 0)
                {
                    key = token.Substring(0, tokenEquals);
                    val = token.Substring(tokenEquals + 1);
                }

                values[key] = val;
            }

            if (!values.ContainsKey("cmd"))
            {
                values["cmd"] = cmd;
            }
            else if (cmd != "cmd")
            {
                values["cmd"] = cmd;
            }

            return true;
        }

        private CommandResult ExecuteCommand(ISemanticProcessor proc, IMembrane membrane, ISemanticType command)
        {
            var result = new CommandResult() { Success = true };

            try
            {
                if (command is CmdUpdateProperty cp)
                {
                    Process(proc, membrane, cp);
                }
                else if (command is CmdShowShape ss)
                {
                    Process(proc, membrane, ss);
                }
                else if (command is CmdGetShapeFiles gs)
                {
                    Process(proc, membrane, gs);
                    result.Response = gs.SerializeResponse();
                }
                else if (command is CmdOutputMessage om)
                {
                    Process(proc, membrane, om);
                }
                else if (command is CmdDropShape ds)
                {
                    Process(proc, membrane, ds);
                }
                else if (command is CmdDropConnector dc)
                {
                    Process(proc, membrane, dc);
                }
                else if (command is CmdClearCanvas cC)
                {
                    Process(proc, membrane, cC);
                }
                else if (command is CmdLoadDiagram ld)
                {
                    Process(proc, membrane, ld);
                }
                else if (command is CmdDeleteShape del)
                {
                    Process(proc, membrane, del);
                }
                else if (command is CmdMoveShape ms)
                {
                    Process(proc, membrane, ms);
                }
                else if (command is CmdConnectShapes con)
                {
                    Process(proc, membrane, con);
                }
                else if (command is CmdListShapes ls)
                {
                    Process(proc, membrane, ls);
                    result.Response = ls.ShapesJson;
                }
                else if (command is CmdNewCanvas newCanvas)
                {
                    Process(proc, membrane, newCanvas);
                }
                else if (command is CmdListCanvases listCanvases)
                {
                    Process(proc, membrane, listCanvases);
                    result.Response = listCanvases.SerializeResponse();
                }
                else if (command is CmdUseCanvas useCanvas)
                {
                    Process(proc, membrane, useCanvas);
                }
                else if (command is CmdSaveWorkspace saveWorkspace)
                {
                    Process(proc, membrane, saveWorkspace);
                }
                else if (command is CmdExportPng exportPng)
                {
                    Process(proc, membrane, exportPng);
                }
                else if (command is CmdSelectShapes selectShapes)
                {
                    Process(proc, membrane, selectShapes);
                }
                else if (command is CmdSelectRegion selectRegion)
                {
                    Process(proc, membrane, selectRegion);
                }
                else if (command is CmdGetSelection getSelection)
                {
                    Process(proc, membrane, getSelection);
                    result.Response = getSelection.SerializeResponse();
                }
                else if (command is CmdMoveSelection moveSelection)
                {
                    Process(proc, membrane, moveSelection);
                }
                else if (command is CmdCopySelection copySelection)
                {
                    Process(proc, membrane, copySelection);
                }
                else if (command is CmdPasteClipboard pasteClipboard)
                {
                    Process(proc, membrane, pasteClipboard);
                }
                else if (command is CmdDeleteSelection deleteSelection)
                {
                    Process(proc, membrane, deleteSelection);
                }
                else if (command is CmdGroupSelection groupSelection)
                {
                    Process(proc, membrane, groupSelection);
                }
                else if (command is CmdUngroupSelection ungroupSelection)
                {
                    Process(proc, membrane, ungroupSelection);
                }
                else if (command is CmdUndo undo)
                {
                    Process(proc, membrane, undo);
                }
                else if (command is CmdRedo redo)
                {
                    Process(proc, membrane, redo);
                }
                else if (command is CmdGetCanvasView getCanvasView)
                {
                    Process(proc, membrane, getCanvasView);
                    result.Response = getCanvasView.SerializeResponse();
                }
                else if (command is CmdSetZoom setZoom)
                {
                    Process(proc, membrane, setZoom);
                }
                else if (command is CmdSetCanvasOffset setCanvasOffset)
                {
                    Process(proc, membrane, setCanvasOffset);
                }
                else if (command is CmdInspectShape inspectShape)
                {
                    Process(proc, membrane, inspectShape);
                    result.Response = inspectShape.SerializeResponse();
                }
                else if (command is CmdRunMacro rm)
                {
                    Process(proc, membrane, rm);
                    result.Response = rm.ResultJson;
                }
                else
                {
                    result.Success = false;
                    result.Error = "Unsupported command '" + command.GetType().Name + "'";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        protected void DeleteShapeHierarchy(BaseController controller, GraphicElement el)
        {
            var childElements = el.GroupChildren.ToList();
            childElements.ForEach(child => DeleteShapeHierarchy(controller, child));
            if (controller.Elements.Contains(el))
            {
                controller.DeleteElement(el);
            }
            else if (el.GroupChildren.Any())
            {
                // Shape was already removed as a child of a parent.
                el.DetachAll();
                el.Connections.ForEach(c => c.ToElement.RemoveConnection(c.ToConnectionPoint.Type));
                el.Connections.Clear();
                el.Dispose();
            }
        }

        protected IEnumerable<GraphicElement> FindShapes(BaseController controller, string id, string name, string text, string type, bool includeConnectors, bool includeChildren = true, bool selectedOnly = false)
        {
            var ret = controller.Elements.AsEnumerable();
            if (!includeConnectors)
            {
                ret = ret.Where(e => !e.IsConnector);
            }

            if (!includeChildren)
            {
                ret = ret.Where(e => e.Parent == null);
            }

            if (!string.IsNullOrWhiteSpace(id) && Guid.TryParse(id, out var guid))
            {
                ret = ret.Where(e => e.Id == guid);
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                ret = ret.Where(e => string.Equals(e.Name, name, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                ret = ret.Where(e => string.Equals(e.Text, text, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                ret = ret.Where(e =>
                    string.Equals(e.GetType().Name, type, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.GetType().FullName, type, StringComparison.OrdinalIgnoreCase));
            }

            if (selectedOnly)
            {
                ret = ret.Where(e => controller.SelectedElements.Contains(e));
            }

            return ret;
        }

        protected GripType ResolveConnectionGrip(IEnumerable<ConnectionPoint> connectionPoints, string text, GripType defaultGrip)
        {
            var points = connectionPoints.ToList();
            if (Enum.TryParse<GripType>(text, true, out var parsed))
            {
                if (points.Any(cp => cp.Type == parsed))
                {
                    return parsed;
                }
            }

            return points.Any(cp => cp.Type == defaultGrip) ?
                defaultGrip :
                points.First().Type;
        }

        protected GripType ResolveConnectorGrip(string text, GripType defaultGrip)
        {
            if (!Enum.TryParse<GripType>(text, true, out var parsed))
            {
                return defaultGrip;
            }

            return parsed == GripType.Start || parsed == GripType.End ? parsed : defaultGrip;
        }

        protected bool TryParseCap(string text, out AvailableLineCap cap)
        {
            cap = AvailableLineCap.None;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return Enum.TryParse(text, true, out cap);
        }

        private void SetConnectorEndpoints(GraphicElement connector, Point p1, Point p2)
        {
            connector.DisplayRectangle = BuildConnectorBounds(p1.X, p1.Y, p2.X, p2.Y);

            if (connector is DynamicConnector dc)
            {
                dc.StartPoint = p1;
                dc.EndPoint = p2;
                dc.DisplayRectangle = BuildConnectorBounds(p1.X, p1.Y, p2.X, p2.Y);
            }
        }

        private static Rectangle BuildConnectorBounds(int x1, int y1, int x2, int y2)
        {
            var left = x1.Min(x2);
            var top = y1.Min(y2);
            var width = x1.Max(x2) - left;
            var height = y1.Max(y2) - top;
            return new Rectangle(left, top, Math.Max(width, 1), Math.Max(height, 1));
        }

        private static int ClampShapeWidth(int width)
        {
            return Math.Max(width, BaseController.MIN_WIDTH);
        }

        private static int ClampShapeHeight(int height)
        {
            return Math.Max(height, BaseController.MIN_HEIGHT);
        }

        private static string ResolvePath(string path)
        {
            string expanded = Environment.ExpandEnvironmentVariables(path ?? string.Empty);
            return Path.IsPathRooted(expanded) ? expanded : Path.GetFullPath(expanded);
        }

        private static void RunOnUiThread(IServiceManager serviceManager, Action action)
        {
            var dispatcher = GetDispatcher(serviceManager);

            if (dispatcher == null || dispatcher.IsDisposed)
            {
                return;
            }

            if (dispatcher.InvokeRequired)
            {
                dispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }

        private static T RunOnUiThread<T>(IServiceManager serviceManager, Func<T> func)
        {
            var dispatcher = GetDispatcher(serviceManager);

            if (dispatcher == null || dispatcher.IsDisposed)
            {
                return default;
            }

            if (dispatcher.InvokeRequired)
            {
                return (T)dispatcher.Invoke(func);
            }

            return func();
        }

        private static Control GetDispatcher(BaseController controller)
        {
            return controller.Canvas.FindForm() ?? (Control)controller.Canvas;
        }

        private static Control GetDispatcher(IServiceManager serviceManager)
        {
            var canvasService = serviceManager.Get<IFlowSharpCanvasService>();
            if (canvasService.ActiveController != null)
            {
                return GetDispatcher(canvasService.ActiveController);
            }

            try
            {
                var dockingService = serviceManager.Get<Clifton.WinForm.ServiceInterfaces.IDockingFormService>();
                if (dockingService?.DockPanel != null)
                {
                    return dockingService.DockPanel.FindForm() ?? (Control)dockingService.DockPanel;
                }
            }
            catch
            {
                // Service may not be available in unit-test scenarios.
            }

            return Application.OpenForms.Cast<Form>().FirstOrDefault();
        }

        private static void RunOnUiThread(BaseController controller, Action action)
        {
            var dispatcher = GetDispatcher(controller);

            if (dispatcher.IsDisposed)
            {
                return;
            }

            if (dispatcher.InvokeRequired)
            {
                dispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }

        private static T RunOnUiThread<T>(BaseController controller, Func<T> func)
        {
            var dispatcher = GetDispatcher(controller);

            if (dispatcher.IsDisposed)
            {
                return default;
            }

            if (dispatcher.InvokeRequired)
            {
                return (T)dispatcher.Invoke(func);
            }

            return func();
        }

        private class CommandResult
        {
            public bool Success { get; set; }
            public string Response { get; set; }
            public string Error { get; set; }
        }

        private class MacroResult
        {
            public int Step { get; set; }
            public string Command { get; set; }
            public bool Success { get; set; }
            public string Response { get; set; }
            public string Error { get; set; }
        }

        protected Color GetColor(string colorString)
        {
            if (string.IsNullOrWhiteSpace(colorString))
            {
                return Color.Black;
            }

            Color color;

            // Get the color from its name or an RGB value as hex codes #RRGGBB
            if (colorString[0] == '!')
            {
                color = ColorTranslator.FromHtml("#" + colorString.Substring(1));
            }
            else
            {
                color = Color.FromName(colorString);
            }

            return color;
        }
    }
}
