# Open Feature Plan

Last reviewed: 2026-05-14

This file tracks only open work. Completed items and historical discovery notes are intentionally omitted.

## Source Of Truth

- This file is the current dependency-ordered open backlog.
- `README.md` "What's not implemented" is the public open-feature summary.
- `progress.md` tracks active progress and verification for these open items.

## Current Focus

Finish the remaining partial feature work: richer text/connector editing, targeted redraw behavior, connector conversion/rerouting semantics, snapping integration, layout command wiring, property UX, and broader undo/redo coverage.

## Open Issues

| # | Issue | Status | Notes |
| --- | --- | --- | --- |
| 2 | Shape text improvements | Partial | Word-wrap state and persistence are implemented; text bounds, paragraph justification, and richer layout behavior remain open. |
| 3 | Connector text | Partial | Dynamic connector midpoint labels are implemented; broader connector label editing remains open. |
| 4 | Property-change redraw optimization | Partial | New editable values are exposed through existing property paths; targeted redraw optimization remains limited. |
| 5 | Force V/H connectors | Partial | Orthogonal connector types exist; force-convert/remove-diagonal behavior remains open. |
| 6 | True dynamic connectors | Partial | Dynamic connector render and anchor behavior improved; full rerouting semantics remain open. |
| 7 | Snap shapes to centers and edges | Partial | Center/edge snap delta helper exists; drag integration remains open. |
| 8 | Align selected shapes | Partial | Controller-level edge alignment exists; remaining UI/workflow coverage still needs verification against the final command surface. |
| 9 | Better property UX | Partial | PropertyGrid exposes added values; redesigned property UX remains open. |
| 10 | Undo/redo | Partial | Existing undo/redo remains available; broad command coverage for new feature actions remains open. |

## Open Regression Work

- [ ] Re-run text and connector rendering checks when label editing or text layout changes.
- [ ] Re-run connector checks when anchor, routing, V/H conversion, or dynamic connector behavior changes.
- [ ] Re-run selection, grouping, snapping, and layout checks when drag integration or alignment workflow changes.
