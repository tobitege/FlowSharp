# Task Plan: Implement `open-features.md`

## Goal

Implement the backlog defined in `open-features.md` in dependency order, with every feature tracked as a checkable item and every non-UI feature getting useful tests in `Tests/` before implementation.

## Current Phase

Phase 3

## Phases

### Phase 1: Requirements, Tracking, And Discovery

- [x] Read `open-features.md` and capture the full backlog
- [x] Confirm planning files are created in the repo root
- [x] Identify existing test projects under `Tests/`
- [x] Map backlog items to likely implementation areas in the codebase
- [x] Document findings in `findings.md`
- **Status:** complete

### Phase 2: Milestone 1 - Shell And Document Safety

- [x] 1. Close document prompts to save changes
- [x] 2. Saving drawing updates MRU when a new filename is introduced
- [x] 3. Add panels back in that have been removed: Toolbox, PropertyGrid, code editor
- [x] Add or update tests first for non-UI behavior in items 1-2
- [ ] Record verification for regression group A
- **Status:** in_progress

### Phase 3: Milestone 2 - Selection And Viewport Foundation

- [x] 4. Be able to select alternate shape when there is more than one option at the click point
- [x] 5. Try intersection depth limit of 1 deep
- [ ] 6. Scrollbars for canvas
- [ ] 7. Zoom
- [ ] 8. True drag-from-toolbox-onto-surface
- [x] Add or update tests first for non-UI logic before implementation where practical
- [ ] Record verification for regression group B items that overlap
- **Status:** in_progress

### Phase 4: Milestone 3 - Page And Text Workflow

- [ ] 9. Ruler margins / page boundaries
- [ ] 10. Printing
- [ ] 11. Shape text improvements
- [ ] 12. Connector text
- [ ] 13. Other line caps besides an arrow and diamond
- [ ] 14. Changing property (like end caps) forces a full page redraw
- [ ] Add or update tests first for non-UI logic before implementation where practical
- **Status:** pending

### Phase 5: Milestone 4 - Anchors And Routing

- [ ] 15. Custom defined connection points / custom anchor points
- [ ] 16. Adjust custom connection points intelligently when shape is resized
- [ ] 17. Auto-anchor
- [ ] 18. Force V/H (removes diagonal)
- [ ] 19. Need 3 line connector middle-line repositioning
- [ ] 20. True dynamic connectors
- [ ] Add or update tests first for non-UI logic before implementation where practical
- [ ] Record verification for regression group C
- **Status:** pending

### Phase 6: Milestone 5 - Geometry And Layout Commands

- [ ] 21. Shape rotation
- [ ] 22. Snap shapes to centers and edges
- [ ] 23. Align selected shapes left/top/right/bottom to leftmost/topmost/rightmost/bottommost
- [ ] 24. "Regroup", so shapes can be manipulated and then regrouped
- [ ] Add or update tests first for non-UI logic before implementation where practical
- **Status:** pending

### Phase 7: Milestone 6 - Cross-Cutting UX And History

- [ ] 25. Better property UX
- [ ] 26. Undo/redo
- [ ] Add or update tests first for non-UI logic before implementation where practical
- [ ] Run final regression coverage for affected areas
- **Status:** pending

### Phase 8: Verification And Delivery

- [ ] Verify completed features against `open-features.md`
- [ ] Ensure every implemented item is checked off in this plan
- [ ] Summarize completed work and remaining backlog clearly if any scope remains
- **Status:** pending

## Feature Tracking Matrix

| # | Feature | Type | Tests Before Implementation? | Status |
|---|---------|------|------------------------------|--------|
| 1 | Close document prompts to save changes | Mixed | Yes | Done |
| 2 | Saving drawing updates MRU when a new filename is introduced | Non-UI | Yes | Done |
| 3 | Add panels back in that have been removed | UI | No | Done |
| 4 | Select alternate shape at shared click point | Mixed | Yes | Done |
| 5 | Intersection depth limit of 1 deep | Non-UI | Yes | Done |
| 6 | Scrollbars for canvas | UI foundation | No | Not started |
| 7 | Zoom | Mixed | Yes | Not started |
| 8 | True drag-from-toolbox-onto-surface | UI | No | Not started |
| 9 | Ruler margins / page boundaries | Mixed | Yes | Not started |
| 10 | Printing | Mixed | Yes | Not started |
| 11 | Shape text improvements | Mixed | Yes | Not started |
| 12 | Connector text | Mixed | Yes | Not started |
| 13 | Other line caps besides an arrow and diamond | Non-UI heavy | Yes | Not started |
| 14 | Property change should not force full page redraw | Non-UI heavy | Yes | Not started |
| 15 | Custom defined connection points / custom anchor points | Non-UI heavy | Yes | Not started |
| 16 | Resize-aware custom connection points | Non-UI heavy | Yes | Not started |
| 17 | Auto-anchor | Non-UI heavy | Yes | Not started |
| 18 | Force V/H connectors | Non-UI heavy | Yes | Not started |
| 19 | 3 line connector middle-line repositioning | Mixed | Yes | Not started |
| 20 | True dynamic connectors | Non-UI heavy | Yes | Not started |
| 21 | Shape rotation | Non-UI heavy | Yes | Not started |
| 22 | Snap shapes to centers and edges | Non-UI heavy | Yes | Not started |
| 23 | Align selected shapes | Non-UI heavy | Yes | Not started |
| 24 | Regroup | Mixed | Yes | Not started |
| 25 | Better property UX | UI | No | Not started |
| 26 | Undo/redo | Non-UI heavy | Yes | Not started |

## Key Questions

1. Which existing services or controllers own dirty-state, save flow, and MRU behavior?
2. Which non-UI behaviors already have test seams in `Tests/FlowSharp.Main.Tests` or `Tests/FlowSharp.Http.IntegrationTests`?
3. Which backlog items are already partially implemented and only need completion or restoration?

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Use file-backed planning in repo root | The user explicitly requested the `planning-with-files` workflow |
| Track every feature as a checkbox in `task_plan.md` | The user wants each item to be checkable when implemented |
| Treat non-UI features as test-first work | The user explicitly asked for tests first in `Tests` |
| Start with Milestone 1 discovery and implementation | It matches the dependency order in `open-features.md` and is the lowest-risk entry point |
| Use existing service seams for initial Milestone 1 tests | `FlowSharpEditService`, `MenuController`, `FlowSharpCanvasService`, and `FlowSharpService` already own the behavior we need to verify |

## Errors Encountered

| Error | Attempt | Resolution |
|-------|---------|------------|
| Skill docs referenced a non-existent `templates/` directory | 1 | Located the actual templates under `assets/templates/` and continued |

## Notes

- Update phase status as work moves forward
- Update feature row status from `Not started` to `In progress` and `Done` as items are implemented
- Keep `progress.md` current with test design and verification results
