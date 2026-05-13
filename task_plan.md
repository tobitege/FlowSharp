# Open Feature Plan

Last reviewed: 2026-05-13

This file tracks only open work. Completed items and historical discovery notes are intentionally omitted.

## Source Of Truth

- This file is the current dependency-ordered open backlog.
- `README.md` "What's not implemented" is the public open-feature summary.
- `progress.md` tracks active progress and verification for these open items.

## Current Focus

Viewport foundation remains the next dependency group: scrollbars, full zoom behavior, and drag-from-toolbox coordinate handling.

## Open Issues

| # | Issue | Status | Notes |
| --- | --- | --- | --- |
| 1 | Scrollbars for canvas | Open | Needed before full viewport-aware zoom and drag/drop. |
| 2 | Zoom | Open | Runtime/control plumbing exists, but full UI/rendering/hit-test zoom behavior remains open. |
| 3 | True drag-from-toolbox-onto-surface | Open | Depends on reliable scrolled/zoomed coordinate transforms. |
| 4 | Ruler margins / page boundaries | Open | Depends on zoom and viewport math. |
| 5 | Printing | Open | Depends on page boundaries and rendering scale decisions. |
| 6 | Shape text improvements | Open | Alignment persistence exists; bounds, paragraph justification, and auto-wrap remain open. |
| 7 | Connector text | Open | Should reuse shape text layout decisions where possible. |
| 8 | Other line caps besides an arrow and diamond | Open | Needs model, rendering, serialization, and UI exposure. |
| 9 | Property-change redraw optimization | Open | Target invalidation scope instead of unnecessary full-page redraws. |
| 10 | Custom defined connection points / custom anchor points | Open | Existing tests cover only basic connection-point equality behavior. |
| 11 | Resize-aware custom connection points | Open | Depends on custom anchor model. |
| 12 | Auto-anchor | Open | Depends on custom anchors and resize behavior. |
| 13 | Force V/H connectors | Open | Depends on anchor/routing decisions. |
| 14 | Three-line connector middle-line repositioning | Open | Depends on orthogonal connector routing. |
| 15 | True dynamic connectors | Open | Depends on anchor and routing foundation. |
| 16 | Shape rotation | Open | Affects rendering, bounds, hit testing, handles, connectors, and text. |
| 17 | Snap shapes to centers and edges | Open | Should be built against final geometry model. |
| 18 | Align selected shapes | Open | Needs reliable selection and bounds semantics. |
| 19 | Regroup | Open | Needs stable grouping ownership and connector behavior. |
| 20 | Better property UX | Open | Should follow the main editable feature model. |
| 21 | Undo/redo | Open | Requires broad command coverage across editing actions. |
| 22 | Try intersection depth limit of 1 deep | Open | Imported from `todo.txt`; verify current hit-testing behavior before implementation. |
| 23 | Add panels back in that have been removed: Toolbox, PropertyGrid, code editor | Open | Imported from `todo.txt`; verify startup/docking layout behavior. |
| 24 | Saving drawing updates MRU when a new filename is introduced | Open | Imported from `todo.txt`; verify normal save and save-as flows. |
| 25 | Close document prompts to save changes | Open | Imported from `todo.txt`; verify canvas-tab close and application close paths. |
| 26 | Be able to select alternate shape when there is more than one option at the click point | Open | Verify shared-point selection behavior. |

## Open Regression Work

- [ ] Re-run saving/document lifecycle checks when related save or text work changes.
- [ ] Re-run selection/grouping checks when viewport, grouping, or alignment work changes.
- [ ] Re-run connector checks when anchor, routing, or dynamic connector work changes.
