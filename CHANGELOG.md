# Changelog

<!-- markdownlint-disable MD024 -->

All notable changes to FlowSharp will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0]

### Added

- Close-document save prompts now cover dirty canvas close decisions without prompting for unrelated clean canvases.
- Normal save now updates the MRU list when the active canvas filename is introduced or changed.
- Removed shell panels are restored through the startup workflow for Toolbox, PropertyGrid, and the code editor.
- Selection now cycles overlapping root shapes on repeated clicks and limits normal nested hit-testing to one child level.
- Canvas viewport support now includes scrollbars, zoom-aware world/client coordinate conversion, and true toolbox drag placement on the drawing surface.
- Page boundary and margin state is modeled and rendered with the diagram.
- Print/export rendering support now includes `RenderTo` and `CreatePrintDocument`.
- Shape text now has persisted word-wrap state, and dynamic connectors can render midpoint labels.
- Lines and connectors now support square and round caps in addition to the existing cap styles.
- Custom connection points are persisted as relative points, remain resize-aware, and can be used by the auto-anchor helper.
- Three-line dynamic connectors now support middle-line repositioning.
- Shape rotation now covers state, persistence, rendering, hit-testing, and print/export rendering.
- Regrouping can restore group membership after ungroup/edit workflows.
- Focused regression tests were added for save workflow, dirty-state handling, selection hit-testing, viewport math, toolbox placement, rendering, line caps, custom anchors, rotation, print document creation, auto-anchor, and regrouping.

### Fixed

- `FocusOn` now pans the viewport to the selected element instead of moving diagram elements.
- Print/export rendering no longer depends on the live viewport or zoom state.
- Dynamic connector print/export rendering now draws to the supplied graphics target.
- Toolbox drag creation now waits until the pointer reaches the canvas before creating the shape.
- Rotated shapes now render geometry, text, selection, hit-testing, and invalidation around rotated bounds.

## [1.2.0]

### Added

- Optional Clifton.Core data modules were brought into the solution for app config, encrypted app config, console logging, critical exception logging, email, model binding, model table management, PaperTrail logging, SQL Server access, semantic processing, template engine support, and web interfaces.
- Runtime control commands for canvas orchestration, shape creation and wiring, selection, grouping, clipboard/history operations, inspection, export, workspace save/load, and macro replay.
- HTTP/WebSocket verification flows and a PowerShell REPL client for driving FlowSharp from automation scripts.
- Runtime configuration and command parsing support for consistent command aliases, viewport commands, and verification command dispatch.

### Changed

- The C# editor integration now uses Scintilla instead of the legacy ICSharpDevelop editor.
- CI and release workflows now use current GitHub Actions versions while keeping the shared reusable build workflow.
- Interaction paths are quieter by default after removing noisy route, shape, and redraw trace logging.

### Fixed

- IL3000 warnings were resolved for single-file publish compatibility.
- Text shape vertical alignment now persists and renders top/bottom alignment correctly.
- Saving unnamed secondary canvases no longer throws, and multi-canvas saves preserve all canvases.
- Multi-shape selection, grouped connector movement, auto-grouping dropped shapes, grouped copy/paste, and duplicate connector attachment handling were stabilized.
- Selection and hit-testing now handle overlapping root shapes, nested shape depth limits, empty-canvas clicks, and repeated mouse-up handling more reliably.
- Diagram traversal and redraw intersection handling are iterative and cycle-safe for grouped shapes.
- `ConnectionPoint` equality and inequality now handle nulls without recursion and align hash codes with value equality.
- Save and dirty-state handling now resolve the active canvas filename consistently and refresh MRU entries on normal saves.
- Code editor loading no longer triggers text-change side effects while selected shape code is loaded.
- Bookmark navigation now sorts directly by name and ignores activation when no shape is selected.

### Commits

- `c9999f6` Fix IL3000 warnings — 2 files changed, 8 insertions, 18 deletions
- `03d8083` changelog adopted to v1.1.0 — 1 file changed, 8 insertions, 26 deletions
- `7f24e96` Switch CI build to master — 1 file changed, 2 insertions, 2 deletions
- `c132e5c` Clean up migration artifacts and refresh websocket docs — 18 files changed, 5468 insertions, 5968 deletions
- `c9bb03e` Replace ICSharpDevelop with Scintilla for C# editing — 23 files changed, 158 insertions, 271 deletions
- `b169a43` fix: correct text shape vertical alignment — 3 files changed, 153 insertions, 94 deletions
- `05c73a0` fix: save unnamed secondary canvas without exception — 2 files changed, 118 insertions, 1 deletion
- `6df6aec` fix: preserve all canvases during multi-canvas save — 3 files changed, 83 insertions, 3 deletions
- `22cc09b` fix: restore multi-shape selection behavior — 3 files changed, 76 insertions, 6 deletions
- `248c811` fix: deduplicate connector attachments — 6 files changed, 116 insertions, 5 deletions
- `f775f9c` fix: stabilize grouped connectors and auto-group dropped shapes — 4 files changed, 161 insertions, 2 deletions
- `a68f4ba` fix: restore grouped shape copy-paste behavior — 4 files changed, 100 insertions, 18 deletions
- `e0e158c` docs: update bug status and add ui test checklist — 1 file changed, 52 insertions, 7 deletions
- `c73c929` feat: add optional Clifton.Core data modules and tests — 25 files changed, 2344 insertions, 11 deletions
- `1dfbd27` add AGENTS.md — 1 file changed, 43 insertions
- `0351132` Add runtime REPL and verification control commands — 11 files changed, 2731 insertions, 96 deletions
- `2e27ae1` Expand runtime control verification coverage — 25 files changed, 1459 insertions, 26 deletions
- `4db024c` Update workflow action versions — 3 files changed, 9 insertions, 9 deletions
- `6aa778c` chore: update workflow actions — 4 files changed, 12 insertions, 9 deletions
- `c44292f` Fix selection and diagram interaction crashes — 25 files changed, 1848 insertions, 137 deletions
- `af39c41` Fix test canvas service warning noise — 3 files changed, 51 insertions, 9 deletions
- `52f066a` Update Agents file — 1 file changed, 7 insertions
- `9704d0d` Update README.md with missing details — 1 file changed, 34 insertions, 5 deletions
- `f59bc02` Docs consolidated — 7 files changed, 78 insertions, 853 deletions

## [1.1.0]

This is the first release version for this fork, assuming all previous versions being 1.0.0,
thus starting here with 1.1.0.

### Changed

- Full .NET 8 migration baseline was applied across project files, services, and runtime configuration.
- Startup flow and docking behavior were updated to better handle Explorer launches.
- Main splitter behavior was refined in docking and plugin path test coverage was expanded.

### Added

- New startup and integration test projects under `Tests/FlowSharp.Main.Tests` and `Tests/FlowSharp.Http.IntegrationTests`.
- FlowSharp startup module configuration.
- Migration scaffolding and documentation including `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `GlobalWindowsPlatformAttributes.cs`, `MIGRATION_SUMMARY.md`, and architecture diagrams.
- `test-coverage.ps1` for coverage workflow support.
- Startup/module assets: `FlowSharpCodeModules.xml`, `modules.xml`, `run.ps1`.

### Commits

- `868d2c2` Main splitter tweaks — 4 files changed, 237 insertions, 18 deletions
- `d20dbd8` fix(startup/ui): handle Explorer launches and stabilize dock splitter behavior — 17 files changed, 4343 insertions, 43 deletions
- `d998060` (feat): full .NET 8 migration baseline and runtime validation — 72 files changed, 2720 insertions, 3477 deletions
- `8026baf` Fix FlowSharp startup modules and add run script — 5 files changed, 129 insertions, 11 deletions

## [2022-09-01]

### Changed

- IntelliSense-conformant updates across core libraries, services, shapes, and related project files.
- Additional project and package maintenance updates.
- `README.md` received maintenance updates.

### Fixed

- CodeTester package reference issues.

### Commits

- `fca6d5c` Update 3 — 32 files changed, 616 insertions, 281 deletions
- `b781b39` Update 2: more intellisense conformant code changes — 53 files changed, 3193 insertions, 3576 deletions
- `2b969bd` Project CodeTester packages fix — 2 files changed, 7 insertions, 2 deletions
- `17fbf36` Update README.md — 1 file changed, 2 insertions

## [2022-08-31]

### Added

- Initial repository baseline including core libraries and services:
  - `Clifton.Core`: Extensive framework for assertions, extension methods, model-table management, module management, semantic processing, and service management.
  - `FlowSharpLib`: Core diagramming engine with support for shapes (Diamond, Box, Triangles), connectors, persistence, and undo/redo logic.
  - `FlowSharpService`: Main application services including Canvas, Menu, Mouse Controller, Property Grid, and Toolbox.
  - `FS-HOPE`: Higher Order Programming Environment integration and shapes.
  - `Plugins`: Support for Windows Control shapes and various service extensions (REST, WebSocket, Code Compiler).
  - Scaffolding for `CodeTester`, `Drakon` shapes, and `Scintilla` editor integration.

### Changed

- `README.md` update and initial project structure setup.

### Commits

- `76135de` Update README.md — 1 file changed, 2 insertions
- `e73b63e` Initial update. — 267 files changed, 17281 insertions, 1719 deletions
