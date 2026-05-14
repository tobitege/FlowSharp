/* 
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;

using Newtonsoft.Json;

using Clifton.Core.Semantics;

namespace FlowSharpServiceInterfaces
{
    public class CmdUpdateProperty : ISemanticType
    {
        public string Name { get; set; }
        public string PropertyName { get; set; }
        public string Value { get; set; }
    }

    public class CmdSetShapeProperty : ISemanticType, IHasResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string Type { get; set; }
        public bool All { get; set; }
        public bool IncludeConnectors { get; set; } = true;
        public bool IncludeChildren { get; set; } = true;
        public string PropertyName { get; set; }
        public string Value { get; set; }
        public string ResultJson { get; set; }

        public string SerializeResponse()
        {
            return ResultJson ?? "{}";
        }
    }

    public class CmdShowShape : ISemanticType
    {
        // Options for indicating what shape to show:
        // By ID, Text, or Name
        public string Id { get; set; }
        public string Text { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Return the filenames for shapes that implement IFileBox.
    /// Used, for example, to FTP files to a server.
    /// </summary>
    public class CmdGetShapeFiles : ISemanticType, IHasResponse
    {
        public List<string> Filenames { get; protected set; }

        public CmdGetShapeFiles()
        {
            Filenames = new List<string>();
        }

        public string SerializeResponse()
        {
            return JsonConvert.SerializeObject(Filenames);
        }
    }

    public class CmdOutputMessage : ISemanticType
    {
        public string Text { get; set; }
    }

    public class CmdClearCanvas : ISemanticType { }

    /// <summary>
    /// Place a shape on the drawing.
    /// </summary>
    public class CmdDropShape : ISemanticType
    {
        public string ShapeName { get; set; }       // shape name to drop
        public string Name { get; set; }            // name of shape to assign to Name property
        public int X { get; set; }
        public int Y { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string Text { get; set; }
        public string FillColor { get; set; }
        public string BorderColor { get; set; }
        public string TextColor { get; set; }
        public bool AutoGroup { get; set; } = true;
    }

    public class CmdDropConnector : ISemanticType
    {
        public string ConnectorName { get; set; }
        public string Name { get; set; }
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public string BorderColor { get; set; }
        public string StartCap { get; set; }
        public string EndCap { get; set; }
    }

    public class CmdLoadDiagram : ISemanticType
    {
        public string Filename { get; set; }
    }

    public class CmdNewCanvas : ISemanticType
    {
        public string Name { get; set; }
    }

    public class CmdListCanvases : ISemanticType, IHasResponse
    {
        public string CanvasesJson { get; set; }

        public string SerializeResponse()
        {
            return CanvasesJson ?? "[]";
        }
    }

    public class CmdUseCanvas : ISemanticType
    {
        public int? Index { get; set; }
        public string Name { get; set; }
        public string Filename { get; set; }
    }

    public class CmdSaveWorkspace : ISemanticType
    {
        public string Filename { get; set; }
        public bool SelectionOnly { get; set; }
        public bool RebaseFilenames { get; set; }
    }

    public class CmdExportPng : ISemanticType
    {
        public string Filename { get; set; }
        public bool SelectionOnly { get; set; }
    }

    public class CmdRenderPrintPage : ISemanticType
    {
        public string Filename { get; set; }
        public bool SelectionOnly { get; set; }
        public int Width { get; set; } = 850;
        public int Height { get; set; } = 1100;
        public int Margin { get; set; } = 50;
    }

    public class CmdDeleteShape : ISemanticType
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public bool All { get; set; }
    }

    public class CmdMoveShape : ISemanticType
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public int? Dx { get; set; }
        public int? Dy { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public bool Relative { get; set; } = true;
    }

    public class CmdConnectShapes : ISemanticType
    {
        public string ConnectorName { get; set; }
        public string Name { get; set; }
        public string Source { get; set; }
        public string SourceId { get; set; }
        public string SourceName { get; set; }
        public string SourceText { get; set; }
        public string Target { get; set; }
        public string TargetId { get; set; }
        public string TargetName { get; set; }
        public string TargetText { get; set; }
        public string SourceGrip { get; set; }
        public string TargetGrip { get; set; }
        public string StartCap { get; set; }
        public string EndCap { get; set; }
    }

    public class CmdListShapes : ISemanticType, IHasResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string Type { get; set; }
        public bool IncludeConnectors { get; set; } = true;
        public bool IncludeChildren { get; set; } = true;
        public bool SelectedOnly { get; set; }
        public string ShapesJson { get; set; }

        public string SerializeResponse()
        {
            return ShapesJson ?? "[]";
        }
    }

    public class CmdSelectShapes : ISemanticType
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string Type { get; set; }
        public bool All { get; set; }
        public bool IncludeConnectors { get; set; } = true;
        public bool IncludeChildren { get; set; }
        public string Mode { get; set; } = "replace";
    }

    public class CmdSelectRegion : ISemanticType
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Mode { get; set; } = "replace";
    }

    public class CmdGetSelection : ISemanticType, IHasResponse
    {
        public string SelectionJson { get; set; }

        public string SerializeResponse()
        {
            return SelectionJson ?? "[]";
        }
    }

    public class CmdMoveSelection : ISemanticType
    {
        public int Dx { get; set; }
        public int Dy { get; set; }
        public bool SnapToCentersAndEdges { get; set; }
    }

    public class CmdDragSelection : ISemanticType
    {
        public int Dx { get; set; }
        public int Dy { get; set; }
        public bool SnapToCentersAndEdges { get; set; } = true;
    }

    public class CmdAlignSelection : ISemanticType
    {
        public string Alignment { get; set; }
    }

    public class CmdRotateSelection : ISemanticType
    {
        public int Degrees { get; set; }
    }

    public class CmdCopySelection : ISemanticType { }

    public class CmdPasteClipboard : ISemanticType { }

    public class CmdDeleteSelection : ISemanticType { }

    public class CmdGroupSelection : ISemanticType { }

    public class CmdUngroupSelection : ISemanticType { }

    public class CmdRegroupSelection : ISemanticType
    {
        public string Name { get; set; }
    }

    public class CmdUndo : ISemanticType { }

    public class CmdRedo : ISemanticType { }

    public class CmdConvertConnector : ISemanticType, IHasResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string Type { get; set; }
        public string Orientation { get; set; } = "LeftRight";
        public string ResultJson { get; set; }

        public string SerializeResponse()
        {
            return ResultJson ?? "{}";
        }
    }

    public class CmdRemoveDiagonalConnectors : ISemanticType, IHasResponse
    {
        public string ResultJson { get; set; }

        public string SerializeResponse()
        {
            return ResultJson ?? "{}";
        }
    }

    public class CmdSetCustomConnectionPoints : ISemanticType, IHasResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string Type { get; set; }
        public bool All { get; set; }
        public bool IncludeChildren { get; set; } = true;
        public string Points { get; set; }
        public string ResultJson { get; set; }

        public string SerializeResponse()
        {
            return ResultJson ?? "{}";
        }
    }

    public class CmdGetCanvasView : ISemanticType, IHasResponse
    {
        public string ViewJson { get; set; }

        public string SerializeResponse()
        {
            return ViewJson ?? "{}";
        }
    }

    public class CmdSetZoom : ISemanticType
    {
        public int Zoom { get; set; }
    }

    public class CmdSetCanvasOffset : ISemanticType
    {
        public int? X { get; set; }
        public int? Y { get; set; }
        public int? Dx { get; set; }
        public int? Dy { get; set; }
        public bool Relative { get; set; }
    }

    public class CmdInspectShape : ISemanticType, IHasResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string Type { get; set; }
        public bool All { get; set; }
        public bool IncludeConnectors { get; set; } = true;
        public bool IncludeChildren { get; set; } = true;
        public bool IncludeConnections { get; set; } = true;
        public bool IncludeConnectionPoints { get; set; } = true;
        public string Properties { get; set; }
        public string ShapesJson { get; set; }

        public string SerializeResponse()
        {
            return ShapesJson ?? "[]";
        }
    }

    public class CmdRunMacro : ISemanticType, IHasResponse
    {
        public string Script { get; set; }
        public string Filename { get; set; }
        public bool ContinueOnError { get; set; }
        public string ResultJson { get; set; }

        public string SerializeResponse()
        {
            return ResultJson ?? "[]";
        }
    }

    public class ShapeSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string Type { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsConnector { get; set; }
        public bool Selected { get; set; }
        public bool Visible { get; set; }
        public Guid? ParentId { get; set; }
        public string ParentName { get; set; }
        public string ParentType { get; set; }
        public int GroupChildCount { get; set; }
        public int ConnectionCount { get; set; }
        public int DistinctConnectionCount { get; set; }
    }

    public class CanvasSummary
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Filename { get; set; }
        public bool IsActive { get; set; }
        public int ShapeCount { get; set; }
        public int RootShapeCount { get; set; }
        public int ConnectorCount { get; set; }
        public int SelectedCount { get; set; }
    }

    public class CanvasViewSummary
    {
        public int Zoom { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public int ViewportOriginX { get; set; }
        public int ViewportOriginY { get; set; }
    }

    public class ShapeConnectionSummary
    {
        public Guid ConnectorId { get; set; }
        public string ConnectorName { get; set; }
        public string ConnectorType { get; set; }
        public string ConnectorGrip { get; set; }
        public string ShapeGrip { get; set; }
        public Guid? ConnectedShapeId { get; set; }
        public string ConnectedShapeName { get; set; }
        public string ConnectedShapeType { get; set; }
    }

    public class ShapeConnectionPointSummary
    {
        public string Grip { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsCustom { get; set; }
    }

    public class CommandCountResult
    {
        public int Count { get; set; }
    }

    public class PropertyCommandResult
    {
        public int Count { get; set; }
        public string PropertyName { get; set; }
        public string RedrawMode { get; set; }
        public string Value { get; set; }
    }

    public class ConnectorCommandResult
    {
        public int Count { get; set; }
        public Guid? ConnectorId { get; set; }
        public string ConnectorName { get; set; }
        public string ConnectorType { get; set; }
    }

    public class ShapeDetail : ShapeSummary
    {
        public Dictionary<string, string> Properties { get; set; }
        public List<ShapeConnectionSummary> Connections { get; set; }
        public List<ShapeConnectionPointSummary> ConnectionPoints { get; set; }
        public List<ShapeSummary> Children { get; set; }
    }
}
