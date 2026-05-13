# Open Work Progress

Last reviewed: 2026-05-13

This file tracks progress only for currently open issues. Historical completed work was removed to keep the file resume-friendly.

## Active Area

Viewport foundation is the next open dependency group.

| Issue | Current progress | Next step |
|-------|------------------|-----------|
| Scrollbars for canvas | Open. Canvas offset support exists, but there is no full scrollbar UI/model integration recorded here. | Define viewport origin, virtual extents, and scrollbar synchronization. |
| Zoom | Open. Basic zoom state/runtime commands exist, but full rendering, hit testing, grips, and scrollbar integration remain open. | Design world-to-screen/screen-to-world transforms around the viewport model. |
| True drag-from-toolbox-onto-surface | Open. Depends on scrolled/zoomed coordinate conversion. | Revisit after viewport and zoom behavior are stable. |

## Remaining Open Progress

| Area | Current progress | Next step |
|------|------------------|-----------|
| Page and printing | Open. Ruler margins, page boundaries, and printing are not tracked as implemented. | Start after viewport and zoom behavior are stable. |
| Shape and connector text | Open. Shape `TextAlign` persistence exists, but text bounds, paragraph justification, auto-wrap, and connector labels remain open. | Define text layout behavior and add focused rendering/persistence tests. |
| Connector caps and redraw | Open. Additional line caps and targeted property-change invalidation remain open. | Extend connector cap model, then verify redraw scope. |
| Anchors and routing | Open. Custom anchors, resize-aware anchors, auto-anchor, V/H routing, middle-line dragging, and dynamic connectors remain open. | Start with a custom anchor data model and persistence tests. |
| Geometry and layout | Open. Rotation, snapping, alignment commands, and regroup remain open. | Defer until viewport, selection, and grouping semantics are stable. |
| Property UX and history | Open. Better property UX and broad undo/redo command coverage remain open. | Defer until the underlying edit features are settled. |

## Open Verification Queue

- [ ] Add/update tests before non-UI implementation where practical.
- [ ] Run `dotnet test Tests\FlowSharp.Main.Tests\FlowSharp.Main.Tests.csproj` after main-library changes.
- [ ] Run `dotnet test Tests\FlowSharp.Http.IntegrationTests\FlowSharp.Http.IntegrationTests.csproj` after runtime-control changes.
- [ ] Update `task_plan.md` and this file whenever an open item moves forward.
