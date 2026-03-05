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
