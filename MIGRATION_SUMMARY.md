# FlowSharp .NET 8 Migration Summary

## Status

This file is the current migration status reference for the repository.

Verified on March 5, 2026:

- `FlowSharp.sln` restores and builds successfully in Debug and Release on `.NET 8`.
- `Tests/FlowSharp.Main.Tests` and `Tests/FlowSharp.Http.IntegrationTests` pass.
- Migration smoke checks pass for websocket, plugin loading, dynamic compilation, and HOPE cross-context loading.

The earlier planning draft was removed after the migration moved past it and it no longer reflected repository state.

## What Landed

### Build And Project System

- Added shared build defaults in `Directory.Build.props`.
- Added central package management in `Directory.Packages.props`.
- Pinned the SDK via `global.json`.
- Converted the projects in `FlowSharp.sln` to SDK-style `.csproj`.
- Preserved `App.config` and existing `AssemblyInfo.cs` files for parity-first migration.

### Runtime Modernization

- Replaced legacy AppDomain-based HOPE loading with collectible `AssemblyLoadContext` loading in `FS-HOPE/FlowSharpHopeService/AppDomainRunner.cs`.
- Replaced CodeDom-based dynamic compilation with Roslyn in:
  - `Services/FlowSharpCodeServices/FlowSharpCodeCompilerService/FlowSharpCodeCompilerService.cs`
  - `FS-HOPE/FlowSharpHopeService/HigherOrderProgrammingService.cs`
- Replaced `websocket-sharp` in runtime code with built-in `.NET` websocket APIs in:
  - `Services/FlowSharpWebSocketService`
  - `FlowSharpClient`

### Dependency Work

- Moved package-managed dependencies to `PackageReference`.
- Migrated DockPanel usage to package restore through `DockPanelSuite`.
- Migrated Scintilla usage to the `Scintilla.NET` package.
- Removed the active ICSharpDevelop editor dependency by routing the C# editor through the Scintilla-based editor service.
- Brought `Clifton.Core.TemplateEngine.sln` onto `.NET 8`.

## Validation Commands

These are the commands used to validate the migrated state:

```powershell
dotnet restore FlowSharp.sln
dotnet build FlowSharp.sln -c Debug
dotnet build FlowSharp.sln -c Release
dotnet test Tests\FlowSharp.Main.Tests\FlowSharp.Main.Tests.csproj -c Debug
dotnet test Tests\FlowSharp.Http.IntegrationTests\FlowSharp.Http.IntegrationTests.csproj -c Debug
dotnet run --project FS-HOPE\CodeTester\CodeTester.csproj -- --websocket-smoke
dotnet run --project FS-HOPE\CodeTester\CodeTester.csproj -- --plugin-smoke 3
dotnet run --project FS-HOPE\CodeTester\CodeTester.csproj -- --dynamic-compile-smoke 3
dotnet run --project FS-HOPE\CodeTester\CodeTester.csproj -- --hope-cross-context-smoke 3
```

## Remaining Follow-Up

These are the migration items that still matter technically:

1. WinForms designer behavior still deserves manual UI verification in the main app.
2. LINQ-to-SQL-specific code excluded from `Clifton.Core` remains a redesign task if that functionality is needed again on `.NET 8`.

## Notes

- The migration remains parity-first: `Nullable` and `ImplicitUsings` are still disabled globally.
- The repository already moved beyond the earlier upgrade plan; this summary is intended to be the maintained status document going forward.
