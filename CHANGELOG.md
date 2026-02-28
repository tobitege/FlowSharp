# Changelog

All notable changes to FlowSharp will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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