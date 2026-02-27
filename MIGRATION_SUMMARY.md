# FlowSharp .NET 8 Migration Summary

## Overview

This document summarizes the migration of FlowSharp from .NET Framework 4.7 to .NET 8.

## Completed Work

### Phase 1: Build Foundation (COMPLETED)
- Created `Directory.Build.props` with shared settings
- Created `global.json` to pin SDK version to 8.0.100
- Set `TargetFramework` to `net8.0-windows`
- Disabled `GenerateAssemblyInfo` to preserve existing AssemblyInfo.cs files during migration
- Disabled `ImplicitUsings` and `Nullable` for parity-first migration

### Phase 2: Project Conversion (COMPLETED)
Converted all 40 projects in FlowSharp.sln from legacy .csproj to SDK-style:

#### Core Libraries
- **Clifton.Core** - Converted with exclusions for LINQ to SQL dependencies
- **FlowSharpLib** - Converted successfully
- **Clifton.WinForm.ServiceInterfaces** - Converted
- **Clifton.SemanticProcessorService** - Converted
- **Clifton.DockingFormService** - Converted

#### Service Interfaces
- **FlowSharpServiceInterfaces** - Converted
- **FlowSharpCodeServiceInterfaces** - Converted
- **FlowSharpCodeShapeInterfaces** - Converted
- **FlowSharpHopeServiceInterfaces** - Converted
- **FlowSharpHopeShapeInterfaces** - Converted

#### Services
- FlowSharpService, FlowSharpCanvasService, FlowSharpToolboxService
- FlowSharpPropertyGridService, FlowSharpMouseControllerService
- FlowSharpMenuService, FlowSharpEditService, FlowSharpDebugWindowService
- FlowSharpRestService, FlowSharpWebSocketService
- FlowSharpCodeService, FlowSharpCodeCompilerService, FlowSharpCodeShapes
- FlowSharpCodeDrakonShapes, FlowSharpCodeOutputWindowService
- FlowSharpCodeScintillaEditorService, FlowSharpCodePythonCompilerService
- FlowSharpCodeICSharpDevelopService

#### HOPE Components
- FlowSharpHopeCommon, FlowSharpHopeService
- HopeRunner, HopeRunnerAppDomainInterface
- HopeShapes, FsHopeLib
- StandAloneRunner, CodeTester

#### Client and Plugins
- FlowSharpClient
- PluginExample
- FlowSharpWindowsControlShapes

#### Main Application
- **FlowSharp** - Converted to SDK-style

### Phase 3: Package Management (COMPLETED)
- Migrated packages.config to PackageReference
- Added `Directory.Packages.props` with Central Package Management enabled
- Added central `Microsoft.CodeAnalysis.CSharp` package version (5.0.0) for Roslyn-based compilation
- Newtonsoft.Json upgraded to 13.0.3
- System.IO.Ports package added for serial port support
- WeifenLuo.WinFormsUI.Docking 2.1.0 preserved
- Contained legacy ICSharpDevelop package transitives (`ICSharpCode.Core`, `ICSharpCode.NRefactory`, `Mono.Cecil`) with `PrivateAssets="all"` in `FlowSharpCodeICSharpDevelopService.csproj` to prevent old Roslyn Workspaces dependencies from leaking into app-level restore (removed prior `NU1608` mismatch warnings in `FlowSharp.sln` build)

### Phase 4: Runtime Blockers (PARTIALLY COMPLETED)

#### Addressed:
1. **LINQ to SQL Dependencies** - Excluded files using System.Data.Linq/DataContext:
   - `Clifton.Core.ExtensionMethods\ContextExtensionMethods.cs`
   - `Clifton.Core.ModelTableManagement\**` (entire directory)
   - `Clifton.Core.ServiceInterfaces\IDbContextService.cs`
   - These features are not available in .NET 8 and need redesign for Entity Framework Core

2. **AppDomain API** - Migrated to `AssemblyLoadContext` in `AppDomainRunner.cs`:
   - Replaced unsupported `AppDomain.CreateDomain()` flow with collectible `AssemblyLoadContext` loading
   - Switched HOPE runner/dependency loading from file-path based loading to in-memory stream loading (`LoadFromStream`) to avoid file-backed loader locks on generated assemblies
   - Implemented unload path using `AssemblyLoadContext.Unload()` with GC finalization cycles
   - Fixed unload polling root cause by releasing the local `AssemblyLoadContext` field strong reference before weak-reference GC polling
   - Added shared-contract resolution in custom load context so cross-boundary interface/event types remain compatible
   - Implemented reflection-backed support for `DescribeReceptor`, `DescribeSemanticType`, and `Publish(typeName, json)` so runner behavior is not stubbed on these paths
   - Hardened runner instantiation failure paths so failed loads do not retain collectible contexts (transactional load-context assignment + explicit failure unload/collection)

3. **Dynamic C# compilation** - Migrated from CodeDom to Roslyn in in-scope runtime paths:
   - `Services/FlowSharpCodeServices/FlowSharpCodeCompilerService/FlowSharpCodeCompilerService.cs`
   - `FS-HOPE/FlowSharpHopeService/HigherOrderProgrammingService.cs`
   - Replaced `CSharpCodeProvider` and `CompilerResults` usage with Roslyn (`Microsoft.CodeAnalysis.CSharp`)
   - Fixed Roslyn source parsing to include explicit UTF-8 encoding so debug-symbol emit does not fail at runtime (`ParseText(..., encoding: Encoding.UTF8)`)

4. **TemplateEngine secondary solution** - Migrated `Clifton.Core.TemplateEngine` scope to .NET 8:
   - Converted `Clifton.Core.TemplateEngine.csproj`, `ModelInterface.csproj`, and `Tests.csproj` to SDK-style
   - Migrated `Clifton.Core.TemplateEngine/Compiler.cs` from CodeDom to Roslyn (`CSharpCompilation`)
   - Restored parser token behavior for `@{ ... }` and `@var` syntax in `Parser.cs` / `Constants.cs`
   - Added explicit SDK-glob exclusions so `Tests/**` and `ModelInterface/**` are not compiled into `Clifton.Core.TemplateEngine`
   - Added central test package versions (`Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`, `MSTest.TestFramework`) to `Directory.Packages.props`
   - `Clifton.Core.TemplateEngine.sln` now builds successfully on .NET 8 and TemplateEngine tests pass (`11/11`)

5. **FlowSharp app reference cleanup**:
   - Replaced legacy local reference in `FlowSharp.csproj` (`Libs/Clifton.DockingFormService.dll`) with a project reference to `Clifton.DockingFormService.csproj`
   - Removed dependency on local `Libs/WeifenLuo.WinFormsUI.Docking.dll` from `FlowSharp.csproj` (resolved transitively via project/package graph)
   - Resolved prior `MSB3245` / `MSB3243` warnings in `FlowSharp.sln` Debug build

6. **WebSocket runtime migration**:
   - Migrated `FlowSharpWebSocketService` and `FlowSharpClient` from `websocket-sharp` to built-in .NET `System.Net.WebSockets`
   - Replaced `WebSocketServer`/`WebSocketBehavior` usage with an `HttpListener` + `AcceptWebSocketAsync` server loop in `FlowSharpWebSocketService.cs`
   - Hardened websocket server startup to try multiple listener prefixes (`host IP`, `localhost`, `127.0.0.1`) so environments without URL ACL for host-IP prefixes can still bind
   - Replaced `WebSocketSharp.WebSocket` client usage with `ClientWebSocket` in:
     - `Services/FlowSharpWebSocketService/WebSocketSender.cs`
     - `FlowSharpClient/WebSocketHelpers.cs`
   - Removed legacy local DLL references to `websocket-sharp.dll` from:
     - `Services/FlowSharpWebSocketService/FlowSharpWebSocketService.csproj`
     - `FlowSharpClient/FlowSharpClient.csproj`

10. **Websocket runtime smoke validation**:
   - Added CLI smoke path in `FS-HOPE/CodeTester/Program.cs`:
     - `--websocket-smoke`
   - Added `CodeTester` project reference to `Services/FlowSharpWebSocketService/FlowSharpWebSocketService.csproj`
   - Smoke path performs end-to-end websocket connect/send/receive using:
     - `FlowSharpWebSocketService.StartServer()` (or existing listener if already running)
     - `ClientWebSocket` transport to `/flowsharp`
     - command roundtrip `cmd=CmdGetShapeFiles` with response format validation
   - Verified passing run:
     - `dotnet run --project FS-HOPE/CodeTester/CodeTester.csproj -- --websocket-smoke`

11. **Plugin runtime smoke validation**:
   - Added CLI smoke path in `FS-HOPE/CodeTester/Program.cs`:
     - `--plugin-smoke [cycles]`
   - Added `CodeTester` project reference to `Services/FlowSharpToolboxService/FlowSharpToolboxService.csproj`
   - Smoke path validates repeatable plugin loading via `PluginManager.InitializePlugins()` and confirms plugin shape discovery over multiple cycles
   - For local build layouts where plugin DLLs are not copied beside `FlowSharp.dll`, the smoke path resolves plugin entries from project `bin/Debug/net8.0-windows` outputs and runs with a temporary absolute-path plugin list
   - Verified passing run:
     - `dotnet run --project FS-HOPE/CodeTester/CodeTester.csproj -- --plugin-smoke 3`

12. **Dynamic compilation runtime smoke validation**:
   - Added CLI smoke path in `FS-HOPE/CodeTester/Program.cs`:
     - `--dynamic-compile-smoke [cycles]`
   - Added `CodeTester` project reference to `Services/FlowSharpCodeServices/FlowSharpCodeCompilerService/FlowSharpCodeCompilerService.csproj`
   - Smoke path invokes `FlowSharpCodeCompilerService` Roslyn compile flow over generated source files and verifies:
     - no compile errors in returned Roslyn results
     - output assemblies are emitted successfully
   - Verified passing run:
     - `dotnet run --project FS-HOPE/CodeTester/CodeTester.csproj -- --dynamic-compile-smoke 3`

13. **HOPE cross-context runtime smoke validation**:
   - Added CLI smoke path in `FS-HOPE/CodeTester/Program.cs`:
     - `--hope-cross-context-smoke [cycles]`
   - Smoke path compiles temporary HOPE runner assemblies (including an external dependency assembly) and validates cross-context behaviors through `AppDomainRunner`:
     - load/unload cycles over collectible `AssemblyLoadContext`
     - receptor metadata reflection (`DescribeReceptor`)
     - semantic type metadata reflection including dependency-backed child types (`DescribeSemanticType`)
     - JSON publish path and processing event propagation (`Publish(typeName, json)`)
     - shared contract compatibility (`InstantiateSemanticType` result assignable to `ISemanticType`)
   - Verified passing run:
     - `dotnet run --project FS-HOPE/CodeTester/CodeTester.csproj -- --hope-cross-context-smoke 3`
     - `dotnet run --project FS-HOPE/CodeTester/CodeTester.csproj -- --hope-cross-context-smoke 20`
   - Smoke assertion focuses on functional isolation/contract behavior (load/describe/publish/unload semantics); after stream-based ALC loading, generated test assemblies now clean up without per-cycle lock warnings in the `20`-cycle run.

7. **Scintilla editor dependency migration**:
   - Replaced local `Libs/ScintillaNET.dll` reference in `FlowSharpCodeScintillaEditorService.csproj` with NuGet package `Scintilla.NET`
   - Added central package version `Scintilla.NET` (`5.3.2.9`) to `Directory.Packages.props`
   - Verified `FlowSharpCodeScintillaEditorService` and full `FlowSharp.sln` build successfully with the package-based reference

8. **ICSharpDevelop dependency migration (partial)**:
   - Replaced local references for `ICSharpCode.Core`, `ICSharpCode.NRefactory`, and `Mono.Cecil` with NuGet packages in `FlowSharpCodeICSharpDevelopService.csproj`
   - Added central package versions for:
     - `ICSharpCode.Core` (`1.0.0-preview2`)
     - `ICSharpCode.NRefactory` (`6.0.0-beta1`)
     - `Mono.Cecil` (`0.11.6`)
   - Removed additional local ICSharpDevelop references no longer required for build:
     - `ICSharpCode.Core.Presentation`
     - `ICSharpCode.SharpDevelop`
     - `ICSharpCode.SharpDevelop.Widgets`
     - `ICSharpCode.TreeView`
   - Verified `FlowSharpCodeICSharpDevelopService` and full `FlowSharp.sln` build successfully after partial migration

9. **HOPE runtime smoke validation (AssemblyLoadContext)**:
   - Added a focused CLI smoke path in `FS-HOPE/CodeTester/Program.cs`:
     - `--hope-alc-smoke [cycles]`
   - Added `CodeTester` project references to `FlowSharpHopeService` and `HopeRunner` so the test can directly exercise `AppDomainRunner` against `HopeRunner.dll`
   - Smoke command validates repeated load/unload cycles and basic reflection path stability (`DescribeReceptor`) without UI dependencies
   - Verified passing run:
     - `dotnet run --project FS-HOPE/CodeTester/CodeTester.csproj -- --hope-alc-smoke 5`

#### Remaining Issues:
1. **Local DLL References** - Need migration path:
   - `ICSharpCode.AvalonEdit` (currently tied to legacy `ICSharpCode.CodeCompletion` assembly versioning)
   - `ICSharpCode.CodeCompletion` (no NuGet package available under matching API/assembly identity)
   - Tried and rejected package replacements:
     - `AvalonEdit` `5.0.3` (insufficient compatibility for current target/package graph)
     - `SharpDevelopCodeCompletion` `1.33.2` (does not expose the legacy `ICSharpCode.CodeCompletion` API used by this codebase: `CodeTextEditor`, `CSharpCompletion`, `ICSharpScriptProvider`)
   
## Build Status

### Projects Building Successfully:
- Full `FlowSharp.sln` Debug build succeeds on .NET 8
- Full `FlowSharp.sln` Release build succeeds on .NET 8
- `Clifton.Core.TemplateEngine.sln` Debug build succeeds on .NET 8
- SDK-style conversion is complete
- Package references and central package management work correctly
- Main `FlowSharp.csproj` was fixed to use explicit items (no repository-wide SDK globs)

### Known Build Issues:
- Some local DLL references still need updating for .NET 8 compatibility (`ICSharpCode.AvalonEdit`, `ICSharpCode.CodeCompletion`)
- Legacy WinForms designer files need verification

## Migration Blockers for Future Work

### Phase 4B: AssemblyLoadContext hardening
`AppDomainRunner.cs` now uses `AssemblyLoadContext`, but still needs runtime validation/hardening:
- Failure-path cleanup for runner load is now implemented to avoid leaked collectible contexts on retries
- Added automated repeated load/unload smoke cycles via `CodeTester --hope-alc-smoke` (baseline pass completed)
- Added cross-context dependency and contract smoke via `CodeTester --hope-cross-context-smoke` (baseline pass completed)
- Added additional unload polling hardening in `AppDomainRunner` and revalidated cross-context flow under `20` cycles
- Added stream-based collectible assembly loading in `AppDomainRunner` to remove file-backed load locks during unload stress cycles
- Residual hardening: optional deep profiling around unload timing under sustained long-run/rebuild pressure

### Phase 4C: Local Library Updates
The following libraries need .NET 8 compatible versions:
- Remaining ICSharpDevelop coupling (`ICSharpCode.CodeCompletion` and its tightly-coupled `ICSharpCode.AvalonEdit` version)

## Next Steps

1. **Update Local Libraries** - Resolve remaining ICSharpDevelop coupling (`ICSharpCode.CodeCompletion` + matching `ICSharpCode.AvalonEdit`)
2. **Runtime Hardening** - Optional deeper stress profiling for HOPE unload timing under sustained long-run/rebuild pressure
3. **Stabilization** - Clean up migration notes/TODOs and confirm behavioral parity in key workflows

## Files Modified

### New Files:
- `Directory.Build.props`
- `Directory.Packages.props`
- `global.json`

### Converted Project Files (40+ .csproj files)
All project files in the solution have been converted from legacy format to SDK-style.

### Source Files with Migration Notes:
- `FS-HOPE/FlowSharpHopeService/AppDomainRunner.cs` - `AssemblyLoadContext` migration implemented, failure-path cleanup added; runtime validation remains
- `FS-HOPE/CodeTester/Program.cs` - Added `--hope-alc-smoke` CLI for repeated `AppDomainRunner` load/unload validation
- `FS-HOPE/CodeTester/CodeTester.csproj` - Added HOPE runner/service references for runtime smoke execution
- `Services/FlowSharpWebSocketService/FlowSharpWebSocketService.cs` - Added websocket bind-prefix fallback (`host IP` -> `localhost` -> `127.0.0.1`)
- `FS-HOPE/CodeTester/Program.cs` - Added `--websocket-smoke` CLI for websocket connect/send/receive runtime validation
- `FS-HOPE/CodeTester/CodeTester.csproj` - Added websocket service reference for websocket smoke execution
- `FS-HOPE/CodeTester/Program.cs` - Added `--plugin-smoke` CLI for repeatable plugin loading validation
- `FS-HOPE/CodeTester/CodeTester.csproj` - Added toolbox service reference for plugin smoke execution
- `FS-HOPE/CodeTester/Program.cs` - Added `--dynamic-compile-smoke` CLI for Roslyn runtime compilation validation
- `FS-HOPE/CodeTester/CodeTester.csproj` - Added compiler service reference for dynamic compilation smoke execution
- `Services/FlowSharpCodeServices/FlowSharpCodeCompilerService/FlowSharpCodeCompilerService.cs` - Fixed Roslyn `ParseText` encoding to support debug-symbol emit
- `FS-HOPE/FlowSharpHopeService/HigherOrderProgrammingService.cs` - Fixed Roslyn `ParseText` encoding to support debug-symbol emit
- `FS-HOPE/CodeTester/Program.cs` - Added `--hope-cross-context-smoke` CLI for HOPE cross-context and contract regression checks
- `FS-HOPE/FlowSharpHopeService/AppDomainRunner.cs` - Added collectible load-context unload wait loop (`WeakReference` + GC polling) and fixed local strong-reference retention during unload polling
- `FS-HOPE/FlowSharpHopeService/AppDomainRunner.cs` - Switched collectible runner/dependency loading to `LoadFromStream` to avoid file-backed load locks in HOPE unload cycles
- `Services/FlowSharpCodeServices/FlowSharpCodeICSharpDevelopService/FlowSharpCodeICSharpDevelopService.csproj` - Marked ICSharpDevelop migration packages as `PrivateAssets="all"` to contain transitive legacy Roslyn Workspaces dependencies

## Migration Plan Compliance

This migration follows the plan from `upgrade_to_.net_8_f606a47a.plan.md`:

- ✅ Phase 0: Baseline and Branch Safety
- ✅ Phase 1: Minimal Build Foundation
- ✅ Phase 2: Project Conversion
- ✅ Phase 3: Package Management Modernization
- ⚠️ Phase 4: Runtime Blockers (Partial - stubs added, full redesign pending)
- ⏳ Phase 5: Stabilization and Cleanup (Pending)

## Notes

1. **Parity-First Approach**: Maintained existing code behavior where possible
2. **Minimal Changes**: Only made necessary changes for .NET 8 compatibility
3. **No Nullable/ImplicitUsings**: Kept disabled to minimize code churn
4. **AssemblyInfo Preserved**: Keeping existing AssemblyInfo.cs files during migration
5. **CR/LF Line Endings**: Maintained for C# files as per requirements
