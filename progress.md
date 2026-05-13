# Progress Log

## Session: 2026-03-18

### Phase 1: Requirements, Tracking, And Discovery

- **Status:** complete
- **Started:** 2026-03-18
- Actions taken:
  - Read `open-features.md`
  - Read the `planning-with-files` skill instructions
  - Ran the session catch-up script
  - Confirmed the repo has no existing `task_plan.md`, `findings.md`, or `progress.md`
  - Identified the current test projects under `Tests/`
  - Created repo-root planning files for tracking and execution
  - Mapped Milestone 1 work to `FlowSharpEditService`, `MenuController`, `FlowSharpCanvasService`, and `FlowSharpService`
  - Reviewed the existing save tests in `CanvasSaveTests.cs`
- Files created/modified:
  - `task_plan.md` (created)
  - `findings.md` (created)
  - `progress.md` (created)

### Phase 2: Milestone 1 - Shell And Document Safety

- **Status:** in_progress

- Actions taken
  - Added `MenuControllerSaveWorkflowTests` to lock down active-canvas filename resolution and MRU refresh on normal save
  - Fixed `MenuController` to resolve filenames from the active canvas, refresh MRU on save, and rebuild the recent-files menu
  - Added `EditServiceDirtyStateTests` to verify per-canvas dirty checks ignore unrelated canvases and preserve prompt decisions
  - Added a cancelable docking document-closing event and used it in `FlowSharpService` so closing a canvas tab can prompt and cancel cleanly
  - Restored a default C# editor panel at startup when no editor document exists yet

- Files created/modified
  - `Clifton/Clifton.WinForm/Clifton.WinForm.ServiceInterfaces/Interfaces.cs`
  - `Clifton/Clifton.WinForm/Services/Clifton.DockingFormService/DockingFormService.cs`
  - `Services/FlowSharpCodeServices/FlowSharpCodeService/FlowSharpCodeService.cs`
  - `Services/FlowSharpEditService/FlowSharpEditService.cs`
  - `Services/FlowSharpMenuService/MenuController.cs`
  - `Services/FlowSharpService/FlowSharpService.cs`
  - `Services/FlowSharpServiceInterfaces/Interfaces.cs`
  - `Tests/FlowSharp.Main.Tests/EditServiceDirtyStateTests.cs`
  - `Tests/FlowSharp.Main.Tests/MenuControllerSaveWorkflowTests.cs`

## Test Results

| Test | Input | Expected | Actual | Status |
|------|-------|----------|--------|--------|
| Planning workflow bootstrap | Read backlog and create planning files | Repo has file-backed tracking for features and progress | `task_plan.md`, `findings.md`, and `progress.md` created in repo root | Passed |
| Save workflow tests | `dotnet test Tests/FlowSharp.Main.Tests/FlowSharp.Main.Tests.csproj --filter "MenuControllerSaveWorkflowTests|EditServiceDirtyStateTests"` | New save and dirty-state tests pass | 4 tests passed | Passed |
| Main test suite | `dotnet test Tests/FlowSharp.Main.Tests/FlowSharp.Main.Tests.csproj` | Existing and new main tests pass together | 39 tests passed | Passed |
| Selection hit-testing tests | `dotnet test Tests/FlowSharp.Main.Tests/FlowSharp.Main.Tests.csproj --filter SelectionHitTestingTests` | New selection cycling and depth-limit tests pass | 2 tests passed | Passed |
| Main test suite after Milestone 2 slice | `dotnet test Tests/FlowSharp.Main.Tests/FlowSharp.Main.Tests.csproj` | Existing and new main tests pass together | 41 tests passed | Passed |

### Phase 3: Milestone 2 - Selection And Viewport Foundation

- **Status:** in_progress

- Actions taken
  - Added `SelectionHitTestingTests` for overlapping root-shape cycling and one-level nested hit-testing
  - Added hit-testing helpers in `BaseController` for enumerating selectable shapes and cycling root candidates
  - Updated `MouseController.SelectSingleRootShape()` to cycle overlapping root shapes on repeated single clicks
  - Limited child hit-testing to one nested level for normal selection helpers

- Files created/modified
  - `FlowSharpLib/BaseController.cs`
  - `Services/FlowSharpMouseControllerService/MouseController.cs`
  - `Tests/FlowSharp.Main.Tests/SelectionHitTestingTests.cs`

## Error Log

| Timestamp | Error | Attempt | Resolution |
|-----------|-------|---------|------------|
| 2026-03-18 | Skill docs referenced missing `templates/` path | 1 | Located working templates under `assets/templates/` and continued |

## 5-Question Reboot Check

| Question | Answer |
|----------|--------|
| Where am I? | Phase 3 |
| Where am I going? | Finish Milestone 2 viewport items, then continue through the remaining milestones |
| What's the goal? | Implement the dependency-ordered backlog from `open-features.md` with tracked checkboxes and test-first non-UI work |
| What have I learned? | Shared-point selection can be handled by cycling root candidates, and nested hit-testing can be constrained safely at the controller helper level |
| What have I done? | Set up planning files, implemented Milestone 1, and completed Milestone 2 items 4 and 5 with tests |

---
*Update after completing each phase or encountering errors*
