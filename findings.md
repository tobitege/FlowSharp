# Findings & Decisions

## Requirements

- Implement the backlog in `open-features.md`.
- Use the `planning-with-files` workflow with persistent markdown tracking in the repo root.
- Make every backlog item trackable and checkable when implemented.
- For non-UI features, design useful tests in the `Tests` folder before implementation.
- Follow the dependency order described in `open-features.md`.

## Research Findings

- `open-features.md` already provides a dependency-ordered backlog with six milestones and regression groupings.
- The repository currently has two test projects:
- `Tests/FlowSharp.Main.Tests/FlowSharp.Main.Tests.csproj`
- `Tests/FlowSharp.Http.IntegrationTests/FlowSharp.Http.IntegrationTests.csproj`
- The skill’s `SKILL.md` refers to `templates/`, but this installation stores the templates under `assets/templates/`.
- Milestone 1 centers on these files:
- `Services/FlowSharpEditService/FlowSharpEditService.cs` owns save-point tracking and the current "save changes" decision.
- `Services/FlowSharpMenuService/MenuController.cs` owns the top-level save/save-as flow, the menu service `filename`, and MRU persistence.
- `Services/FlowSharpCanvasService/FlowSharpCanvasService.cs` owns multi-canvas save ordering and filename rebasing.
- `Services/FlowSharpService/FlowSharpService.cs` owns application close handling and dock-document close integration.
- Existing tests already cover save-related behavior in `Tests/FlowSharp.Main.Tests/CanvasSaveTests.cs`, which is the best place to extend non-UI save workflow coverage.
- `FlowSharpEditService.CheckForChanges()` currently checks whether any controller is dirty, then prompts once with no canvas-specific context.
- `MenuController.SaveAs()` updates MRU, but the regular save path through `SaveDiagram()` does not.
- `MenuController` also stores a single service-level `filename`, while each canvas controller carries `BaseController.Filename`; that mismatch is likely part of the MRU/save bug in multi-canvas cases.
- `defaultLayout.xml` still includes Toolbox and PropertyGrid content entries, and `FlowSharpService.OnContentLoaded()` still rebuilds those panels.
- The docking service used a non-cancelable `DocumentClosing` event, so per-canvas close prompts required a cancelable event args type and propagation back into `FormClosingEventArgs.Cancel`.
- The code editor services were still present and wired through `FlowSharpCodeService`, but startup was not restoring an editor document by default.
- Selection is driven by `BaseController.GetRootShapeAt()` / `GetChildShapeAt()` and `MouseController.SelectSingleRootShape()`.
- A safe way to implement alternate shape selection is to cycle overlapping root shapes on repeated single clicks at the same point without changing multi-select behavior.
- For the "1 deep" hit-testing rule, limiting selectable nested shapes to depth 1 keeps root shapes plus direct children selectable while excluding deeper descendants from normal hit-testing.

## Technical Decisions

| Decision | Rationale |
|----------|-----------|
| Use `task_plan.md` as the canonical checklist for all backlog items | It keeps feature tracking visible and easy to update during implementation |
| Keep detailed discoveries in `findings.md` and execution/test logs in `progress.md` | This follows the skill workflow and makes resuming easier |
| Start discovery around Milestone 1 before touching code | The document order is dependency-first and minimizes rework |
| Begin with service-level tests for save and close behavior | They are useful non-UI tests and do not require full UI automation |
| Treat per-canvas filename consistency as part of Milestone 1 | It affects both close/save prompts and MRU updates |
| Use a cancelable docking close event instead of ad hoc state checks after the fact | Canvas-tab close needs a real way to stop the document from closing |
| Restore the code editor panel via `FlowSharpCodeService.OnFlowSharpInitialized` if no editor document exists | Toolbox and PropertyGrid already came back from the default layout, so this fills the remaining shell gap with minimal risk |
| Implement alternate shared-point selection as root-shape cycling on repeated clicks | It solves the ambiguity problem with minimal disruption to existing mouse routes |
| Apply the depth limit in hit-testing, not redraw recursion | This matches the backlog note about grouping traversal and keeps redraw risk lower |

## Issues Encountered

| Issue | Resolution |
|-------|------------|
| Planning skill file paths were stale | Resolved by inspecting the installed skill directory and using `assets/templates` as the source of truth |

## Resources

- `open-features.md`
- `C:/Users/tobias/.codex/skills/planning-with-files/SKILL.md`
- `Tests/FlowSharp.Main.Tests/FlowSharp.Main.Tests.csproj`
- `Tests/FlowSharp.Http.IntegrationTests/FlowSharp.Http.IntegrationTests.csproj`
- `Services/FlowSharpEditService/FlowSharpEditService.cs`
- `Services/FlowSharpMenuService/MenuController.cs`
- `Services/FlowSharpCanvasService/FlowSharpCanvasService.cs`
- `Services/FlowSharpService/FlowSharpService.cs`
- `Tests/FlowSharp.Main.Tests/CanvasSaveTests.cs`

## Visual/Browser Findings

- None yet

---
*Update this file after every 2 view/browser/search operations*
*This prevents visual information from being lost*
