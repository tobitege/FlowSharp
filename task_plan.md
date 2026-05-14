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
| 8 | Align selected shapes | Partial | Controller-level edge alignment exists; remaining UI/workflow coverage still needs verification against the final command surface. |
| 9 | Better property UX | Partial | PropertyGrid exposes added values; redesigned property UX remains open. |
| 10 | Undo/redo | Partial | Existing undo/redo remains available; broad command coverage for new feature actions remains open. |

## Open Regression Work

- [ ] Re-run connector checks when anchor, routing, V/H conversion, or dynamic connector behavior changes.
- [ ] Re-run selection, grouping, snapping, and layout checks when drag integration or alignment workflow changes.
