# FlowSharp

![FlowSharp](https://github.com/cliftonm/FlowSharp/blob/master/Article/flowsharp2.png)

FlowSharp is a WinForms-based diagramming environment. The main solution now targets `.NET 8` on Windows.

## Current Status

- `FlowSharp.sln` builds in Debug and Release on `.NET 8`.
- Automated tests live under `Tests/`.
- Migration-specific smoke checks live in `FS-HOPE/CodeTester`.
- [`MIGRATION_SUMMARY.md`](MIGRATION_SUMMARY.md) is the current migration status reference.

## Documentation

[Article describing object model, code, and example usage.](https://cdn.rawgit.com/cliftonm/FlowSharp/master/Article/index2.htm)

## Requirements To Build

- Windows
- `.NET 8` SDK
- Visual Studio 2022 or the `dotnet` CLI

## Build

```powershell
dotnet restore FlowSharp.sln
dotnet build FlowSharp.sln -c Debug
dotnet test Tests\FlowSharp.Main.Tests\FlowSharp.Main.Tests.csproj -c Debug
dotnet test Tests\FlowSharp.Http.IntegrationTests\FlowSharp.Http.IntegrationTests.csproj -c Debug
```

## Runtime Control Interface

FlowSharp exposes a remote command API while running:

- HTTP: `http://localhost:8001/flowsharp`
- WebSocket: `ws://localhost:1100/flowsharp/`

Commands are sent as query string style data with `cmd=<command>`.

### Command Groups

The runtime command set is organized around the verification work in `todo.txt`, from least-needed plumbing up to full interaction replay.

1. Canvas orchestration

```text
listcanvases
newcanvas Name="Second Canvas"
usecanvas Index=1
saveworkspace Filename=C:\temp\verify\diagram.fsd RebaseFilenames=true
exportpng Filename=C:\temp\verify\canvas.png
loaddiagram Filename=C:\temp\verify\diagram.fsd
clearcanvas
```

2. Shape CRUD and wiring

```text
dropshape ShapeName=Box Name=Start X=100 Y=120 Text="Start"
dropshape ShapeName=Box Name=InsideGroup X=160 Y=180 AutoGroup=true
dropconnector ConnectorName=DiagonalConnector Name=Wire X1=100 Y1=100 X2=240 Y2=180
connectshapes Source=Start Target=End SourceGrip=RightMiddle TargetGrip=LeftMiddle ConnectorName=DiagonalConnector
moveshape Name=Start Dx=0 Dy=80
deleteshape Name=End
updateproperty Name=Start PropertyName=TextAlign Value=TopLeft
```

`dropshape` now auto-groups by default when the dropped shape lands fully inside a group box, matching the toolbox behavior.

3. Selection and movement

```text
selectshapes Name=Start
selectshapes Name=End Mode=add
selectregion X=80 Y=80 Width=260 Height=140
getselection
moveselection Dx=40 Dy=0
```

4. Clipboard, grouping, and history

```text
groupselection
ungroupselection
copyselection
pasteclipboard
undo
redo
```

5. Inspection and assertions

```text
listshapes IncludeConnectors=true
listshapes SelectedOnly=true
inspectshape Name=Start Properties=TextAlign,DisplayRectangle
inspectshape Name=DockPanelSuiteServices
```

`inspectshape` returns JSON with common properties, parent/group info, child summaries, and connector attachment details. This is the command to use when verifying persisted `TextAlign`, auto-group state, or duplicate connector attachments.

### Macro Language

`cmd=runmacro` accepts either inline `Script` or a `Filename` containing one command per line.
Commands execute synchronously and each step returns success/error metadata.

```text
dropshape ShapeName=Box Name=Start X=100 Y=120 Text="line 1\nline 2"
updateproperty Name=Start PropertyName=TextAlign Value=BottomCenter
newcanvas Name="Verification 2"
listcanvases
saveworkspace Filename=C:\temp\verify\diagram.fsd RebaseFilenames=true
usecanvas Index=0
selectshapes Name=Start
groupselection
copyselection
pasteclipboard
moveselection Dx=40 Dy=0
inspectshape Name=Start
```

Command aliases and legacy `Cmd` prefixes are accepted. Separators are ignored, so `listcanvases`, `list-canvases`, `list_canvases`, and `CmdListCanvases` all resolve to the same command type.

Macro call examples:

- HTTP

```text
http://localhost:8001/flowsharp?cmd=runmacro&continueonerror=true&script=dropshape%20ShapeName%3DBox%20Name%3DStart%20X%3D100%20Y%3D120%0Ainspectshape%20Name%3DStart
```

- WebSocket message

```text
cmd=runmacro&filename=C%3A%5Ctemp%5Cverify.flow
```

`listcanvases`, `listshapes`, `getselection`, `inspectshape`, and `runmacro` return JSON. Other commands return `OK` on the HTTP endpoint and an empty WebSocket payload unless they fail.

### REPL

A local REPL client is included:

```powershell
powershell -ExecutionPolicy Bypass -File tools\FlowSharpRepl.ps1
```

Useful REPL forms:

```text
:load C:\temp\verify.flow
listcanvases
selectshapes Name=ModuleManagement
inspectshape Name=DockPanelSuiteServices Properties=TextAlign,DisplayRectangle
```

The REPL is a thin WebSocket client over the same runtime interface. It is meant for remote control and debugging while the WinForms app is open.

### Mapping To `Verify completed bug fixes:`

- Text alignment: `dropshape` or `selectshapes` + `updateproperty` + `saveworkspace` + `loaddiagram` + `inspectshape`
- Text alignment rendering capture: `exportpng` after top-aligned and bottom-aligned states
- Save unnamed second canvas: `newcanvas` + `listcanvases` + `saveworkspace RebaseFilenames=true`
- Save multiple canvases without losing content: `usecanvas` + shape CRUD on each canvas + `saveworkspace` + `loaddiagram` + `listcanvases`/`listshapes`
- Multi-select: `selectshapes Mode=add`, `selectregion`, `getselection`, `moveselection`
- Duplicate connector attachment: `moveshape` + `inspectshape` on the grouped shape to compare `ConnectionCount` and `DistinctConnectionCount`
- Group move and auto-group on drop: `dropshape AutoGroup=true` into a group box + `inspectshape` on the dropped shape and group box
- Grouped copy/paste with undo/redo: `groupselection` + `copyselection` + `pasteclipboard` + `moveselection` + `undo` + `redo`

## Optional Smoke Checks

```powershell
dotnet run --project FS-HOPE\CodeTester\CodeTester.csproj -- --websocket-smoke
dotnet run --project FS-HOPE\CodeTester\CodeTester.csproj -- --plugin-smoke 3
dotnet run --project FS-HOPE\CodeTester\CodeTester.csproj -- --dynamic-compile-smoke 3
dotnet run --project FS-HOPE\CodeTester\CodeTester.csproj -- --hope-cross-context-smoke 3
```

# Features
A short list of some of the features.

## Virtual Surface
![Virtual Surface](https://github.com/cliftonm/FlowSharp/blob/master/Article/img1.png)

## Efficient rendering of shapes
![Rendering](https://github.com/cliftonm/FlowSharp/blob/master/Article/img2.png)

## Z-ordering
![Z-order](https://github.com/cliftonm/FlowSharp/blob/master/Article/img3.png)

## Text for shapes
![Text](https://github.com/cliftonm/FlowSharp/blob/master/Article/img4.png)

## Export diagram to PNG
![PNG](https://github.com/cliftonm/FlowSharp/blob/master/Article/img5.png)

## Connection points and grips
![Virtual Surface](https://github.com/cliftonm/FlowSharp/blob/master/Article/img8.png)

## Copy and paste between diagrams
![Virtual Surface](https://github.com/cliftonm/FlowSharp/blob/master/Article/img11.png)

## Anchor drag snapping
![Virtual Surface](https://github.com/cliftonm/FlowSharp/blob/master/Article/snapping.png)

## Grouping
![Grouping](https://github.com/cliftonm/FlowSharp/blob/master/Article/img36.png)

# What's not implemented:
Please contribute to working on this list!
* Shape text:
  * Currently only centered in the shape.
  * Boundaries can be easily exceeded.
  * No justification.
  * Single line only - no auto-wrap.
* Scrollbars for canvas - currently you drag the canvas to move it.
* Zoom.
* Shape rotation.
* Custom defined connection points.
  * Including on connectors.
* Adjust custom connection points intelligently when shape is resized.
* True dynamic connectors.
* Other line caps besides an arrow and diamond.
* Ruler margins / page boundaries.
* Snap shapes to centers and edges.
* Printing (more or less easily implemented, actually)  If you want to print, save the diagram as a PNG and use some other tool!
* True drag-from-toolbox-onto-surface.
* Undo/redo.  That'll be fun!
* Better property UX - PropertyGrid's are ok for developers, they are awful for users.

# License
[The Code Project Open License (CPOL) 1.02](http://htmlpreview.github.io/?http://www.codeproject.com/info/cpol10.aspx)
