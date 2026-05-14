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

FlowSharp can be controlled from outside the UI while it is running. In plain terms, this lets a script do the same kinds of things a user would normally do by hand: create canvases, drop shapes, connect them, move selections, group items, inspect diagram state, save workspaces, and export images.

This is useful for repeatable bug verification, smoke testing, demos, and remote debugging. Instead of manually clicking through a scenario every time, you can send commands over HTTP, WebSocket, the included PowerShell REPL, or a saved macro file and get predictable results back from the live application.

The runtime control API is available through:

- HTTP: `http://localhost:8001/flowsharp`
- WebSocket: `ws://localhost:1100/flowsharp/`

The default `modules.xml` starts the normal desktop app without runtime-control listeners. For runtime/REPL automation, launch FlowSharp with the runtime-control module set:

```powershell
$env:FLOWSHARP_MACRO_STEP_DELAY_MS = "0"
.\bin\Debug\net8.0-windows\FlowSharp.exe .\bin\Debug\net8.0-windows\FlowSharpRuntimeControlModules.xml
```

Commands are sent as query string style data with `cmd=<command>`. For example, `cmd=listcanvases` asks the running app which canvases are open, while commands like `dropshape`, `connectshapes`, and `inspectshape` drive and observe the diagram.

### Command Groups

The runtime command set is organized from basic canvas control up to full interaction replay.

1. Canvas orchestration

```text
listcanvases
newcanvas Name="Second Canvas"
usecanvas Index=1
getcanvasview
setzoom Zoom=80
setcanvasoffset X=15 Y=5
setcanvasoffset Relative=true Dx=40 Dy=0
saveworkspace Filename=C:\temp\verify\diagram.fsd RebaseFilenames=true
exportpng Filename=C:\temp\verify\canvas.png
renderprintpage Filename=C:\temp\verify\print-page.png Width=850 Height=1100 Margin=50
loaddiagram Filename=C:\temp\verify\diagram.fsd
clearcanvas
```

2. Shape CRUD and wiring

```text
dropshape ShapeName=Box Name=Start X=100 Y=120 Text="Start"
dropshape ShapeName=Box Name=InsideGroup X=160 Y=180 AutoGroup=true
dropconnector ConnectorName=DiagonalConnector Name=Wire X1=100 Y1=100 X2=240 Y2=180 StartCap=Arrow EndCap=Diamond
connectshapes Source=Start Target=End SourceGrip=RightMiddle TargetGrip=LeftMiddle ConnectorName=DiagonalConnector
moveshape Name=Start Dx=0 Dy=80
deleteshape Name=End
updateproperty Name=Start PropertyName=TextAlign Value=TopLeft
setproperty Name=Start PropertyName=TextBounds Value="20, 15, 130, 70"
setcustomconnectionpoints Name=Start Points=Center:5000,2500
```

`dropshape` now auto-groups by default when the dropped shape lands fully inside a group box, matching the toolbox behavior.
Shape placement is coordinate-based: `dropshape` uses `X`/`Y` plus optional `Width`/`Height`, and `dropconnector` uses `X1`/`Y1`/`X2`/`Y2`.
`getcanvasview` returns the active canvas zoom, tracked canvas offset, and viewport origin.
`setcanvasoffset` translates the current root content under FlowSharp's existing canvas-drag model; it is not a separate camera transform.

3. Selection and movement

```text
selectshapes Name=Start
selectshapes Name=End Mode=add
selectregion X=80 Y=80 Width=260 Height=140
getselection
moveselection Dx=40 Dy=0
dragselection Dx=-2 Dy=0 SnapToCentersAndEdges=true
deleteselection
alignselection Alignment=Left
rotateselection Degrees=30
```

4. Clipboard, grouping, and history

```text
groupselection
ungroupselection
regroupselection Name=GroupRoot
copyselection
pasteclipboard
undo
redo
convertconnector Name=Flow Orientation=LeftRight
removediagonalconnectors
```

5. Inspection and assertions

```text
listshapes IncludeConnectors=true
listshapes SelectedOnly=true
inspectshape Name=Start Properties=TextAlign,DisplayRectangle
inspectshape Name=DockPanelSuiteServices
showshape Name=Start
getshapefiles
outputmessage Text="Verification complete"
```

`inspectshape` returns JSON with common properties, parent/group info, child summaries, and connector attachment details. This is the command to use when verifying persisted `TextAlign`, auto-group state, or duplicate connector attachments.

6. Macro execution

```text
runmacro Filename=C:\temp\verify.flow
runmacro ContinueOnError=true Script="dropshape ShapeName=Box Name=Start X=100 Y=120"
```

### Macro Language

`cmd=runmacro` accepts either inline `Script` or a `Filename` containing one command per line.
Commands execute synchronously and each step returns success/error metadata.
Macro playback is throttled by default with a `500ms` pause between commands so the app remains observable while it runs.
Set `FLOWSHARP_MACRO_STEP_DELAY_MS=0` to disable the delay, or set it to another non-negative millisecond value before launching FlowSharp.

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

The REPL defaults to `ws://localhost:1100/flowsharp/`. To target a runtime launched on a different port, pass `-Uri`, set `FLOWSHARP_REPL_URI`, or set `FLOWSHARP_WEBSOCKET_PORT`.

Useful REPL forms:

```text
:load C:\temp\verify.flow
listcanvases
selectshapes Name=ModuleManagement
inspectshape Name=DockPanelSuiteServices Properties=TextAlign,DisplayRectangle
```

The REPL is a thin WebSocket client over the same runtime interface. It is meant for remote control and debugging while the WinForms app is open.

The repo also includes reusable macro files for the seven `Verify completed bug fixes:` flows:

```text
tools\repl-scripts\bug-review\01-text-alignment.flow
tools\repl-scripts\bug-review\02-save-unnamed-second-canvas.flow
tools\repl-scripts\bug-review\03-save-multiple-canvases.flow
tools\repl-scripts\bug-review\04-multi-select.flow
tools\repl-scripts\bug-review\05-duplicate-connector-attachment.flow
tools\repl-scripts\bug-review\06-group-move-and-autogroup.flow
tools\repl-scripts\bug-review\07-grouped-copy-paste.flow
tools\repl-scripts\bug-review\08-runtime-feature-surfaces.flow
```

The runtime feature-surface script has a fast verifier that uses the same REPL client. With no URI supplied, it starts FlowSharp with `FlowSharpRuntimeControlModules.xml` on dynamic ports, runs the macro, and shuts FlowSharp down:

```powershell
powershell -ExecutionPolicy Bypass -File tools\repl-scripts\bug-review\Test-RuntimeFeatureSurfaces.ps1
```

### Mapping To `Verify completed bug fixes:`

- Text alignment: `dropshape` or `selectshapes` + `updateproperty` + `saveworkspace` + `loaddiagram` + `inspectshape`
- Text alignment rendering capture: `exportpng` after top-aligned and bottom-aligned states
- Save unnamed second canvas: `newcanvas` + `listcanvases` + `saveworkspace RebaseFilenames=true`
- Save multiple canvases without losing content: `usecanvas` + shape CRUD on each canvas + `saveworkspace` + `loaddiagram` + `listcanvases`/`listshapes`
- Multi-select: `selectshapes Mode=add`, `selectregion`, `getselection`, `moveselection`
- Duplicate connector attachment: `moveshape` + `inspectshape` on the grouped shape to compare `ConnectionCount` and `DistinctConnectionCount`
- Group move and auto-group on drop: `dropshape AutoGroup=true` into a group box + `inspectshape` on the dropped shape and group box
- Grouped copy/paste with undo/redo: `groupselection` + `copyselection` + `pasteclipboard` + `moveselection` + `undo` + `redo`
- Text bounds, paragraph justification, word wrap, connector label layout, line caps, rotation, and property-grid redraw policy: `setproperty` or `dropconnector StartCap`/`EndCap` + `inspectshape` + `undo`/`redo`
- Print renderer verification: `renderprintpage`
- Orthogonal connector conversion and diagonal removal: `convertconnector Orientation=LeftRight|UpDown` + `removediagonalconnectors` + `inspectshape` + `undo`/`redo`
- Center/edge drag snapping and align workflow actions: `dragselection SnapToCentersAndEdges=true` + `alignselection` + `listshapes`
- Custom connection points: `setcustomconnectionpoints` + `inspectshape IncludeConnectionPoints=true`, including resize and save/load persistence checks
- Regroup: `ungroupselection` + `regroupselection` + `inspectshape`
- Dynamic rerouting: `connectshapes` with a dynamic connector + `moveshape`/geometry changes + `inspectshape` connection grips
- Focus/pan behavior: `showshape` + `getcanvasview` viewport-origin assertions

Current caveat: the text-alignment script/test is still a proxy check. It verifies `TextAlign`, persistence after reload, and that top/bottom PNG exports differ, but it does not yet perform pixel-level validation that the text is visually near the top versus near the bottom.

## Optional Smoke Checks

```powershell
dotnet run --project FS-HOPE\CodeTester\CodeTester.csproj -- --websocket-smoke
dotnet run --project FS-HOPE\CodeTester\CodeTester.csproj -- --plugin-smoke 3
dotnet run --project FS-HOPE\CodeTester\CodeTester.csproj -- --dynamic-compile-smoke 3
dotnet run --project FS-HOPE\CodeTester\CodeTester.csproj -- --hope-cross-context-smoke 3
```

# Features
A short list of some of the features.

## Runtime control and automation

FlowSharp can be controlled while running through HTTP and WebSocket commands. The command surface supports canvas orchestration, shape creation and wiring, selection, grouping, clipboard/history operations, inspection, PNG export, and workspace save/load flows.

## REPL, macros, and verification scripts

The repository includes `tools\FlowSharpRepl.ps1`, a PowerShell REPL client for the runtime control API. Commands can also be replayed as macros from inline script text or `.flow` files, and reusable bug-review scripts live under `tools\repl-scripts\bug-review\`.

## Virtual Surface
![Virtual Surface](https://github.com/cliftonm/FlowSharp/blob/master/Article/img1.png)

## Efficient rendering of shapes
![Rendering](https://github.com/cliftonm/FlowSharp/blob/master/Article/img2.png)

## Z-ordering
![Z-order](https://github.com/cliftonm/FlowSharp/blob/master/Article/img3.png)

## Text for shapes

Shape text supports wrapped multiline layout, local text bounds, text margins, paragraph justification, and top, middle, and bottom vertical alignment combined with left, center, and right horizontal alignment. Text layout settings are persisted with diagrams.

![Text](https://github.com/cliftonm/FlowSharp/blob/master/Article/img4.png)

## Connector labels

Connectors support editable labels with persisted label size and offset, so connector text can be positioned away from the midpoint when needed.

## Orthogonal connector conversion

Diagonal connectors can be converted to left-right or up-down orthogonal dynamic connectors while preserving labels, caps, endpoints, and attached shape connections.

Dynamic connectors reroute to facing shape anchors when connected shapes move or their geometry changes, and the stored attachment metadata follows the new anchor points.

Dragging selected shapes snaps nearby centers and edges to other visible shapes.

The Align menu exposes left, right, top, and bottom selected-shape alignment commands with undo/redo support.

The property grid uses categorized friendly names, descriptions, and read-only metadata for shape type fields to make common shape, text, connector, and styling properties easier to identify.

Undo/redo covers the new property-grid layout and connector label edits, align menu actions, and orthogonal connector conversion.

## Export diagram to PNG
![PNG](https://github.com/cliftonm/FlowSharp/blob/master/Article/img5.png)

## Print workflow

The File menu includes a Print command that opens the Windows print dialog and prints the active diagram through the same viewport-independent renderer used by export.

## Multi-canvas workspace saves

Workspaces can save multiple canvases. Unnamed secondary canvases are assigned sibling filenames such as `diagram-1.fsd`, including when the base filename is relative.

## Connection points and grips
![Virtual Surface](https://github.com/cliftonm/FlowSharp/blob/master/Article/img8.png)

## Copy and paste between diagrams
![Virtual Surface](https://github.com/cliftonm/FlowSharp/blob/master/Article/img11.png)

## Anchor drag snapping
![Virtual Surface](https://github.com/cliftonm/FlowSharp/blob/master/Article/snapping.png)

## Grouping
![Grouping](https://github.com/cliftonm/FlowSharp/blob/master/Article/img36.png)

## What's Not Implemented

Please contribute to working on this list!

- Connector rendering, anchors, and routing:
  - Custom defined connection points / custom anchor points, including on connectors.
  - Adjust custom connection points intelligently when shape is resized.
  - Auto-anchor.
  - Three-line connector middle-line repositioning.
- Geometry and layout commands:
  - Shape rotation.
  - Regroup, so shapes can be manipulated and then regrouped.
- Cross-cutting UX and history:

# License
[The Code Project Open License (CPOL) 1.02](http://htmlpreview.github.io/?http://www.codeproject.com/info/cpol10.aspx)
