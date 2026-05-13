# Open Features And Todo Plan

This document merges the open feature list from `README.md` with the active backlog and manual regression notes from `todo.txt`.
The result is a single implementation roadmap ordered by dependency first and by estimated complexity second.

## Sources

- `README.md` -> `# What's not implemented:`
- `todo.txt` -> `TODO:`
- `todo.txt` -> `Human UI Testing:`
- `todo.txt` -> completed bug fixes kept as regression context, not active backlog

## Planning rules

- Implement prerequisites before dependent work.
- Prefer low-risk shell and document workflow fixes early.
- Finish viewport and coordinate-system work before placement and page features.
- Finish anchor and routing primitives before advanced connector behavior.
- Leave broad cross-cutting systems until the underlying edit model is stable.

## Normalized backlog

Some items overlap and are tracked here as one epic:

- `Custom defined connection points` and `Custom anchor points` are treated as one feature area.
- `True dynamic connectors`, `Auto-anchor`, `Force V/H`, and `Need 3 line connector middle-line repositioning` are treated as one connector-routing track with internal ordering.
- `Better property UX` is kept separate from `Add panels back in that have been removed`, because restoring panels is a shell/integration task while better property UX is a later redesign task.

## Main dependency chains

- `Add panels back in` -> `True drag-from-toolbox-onto-surface` -> `Better property UX`
- `Scrollbars for canvas` -> `Zoom` -> `Ruler margins / page boundaries` -> `Printing`
- `Custom anchor points` -> `Adjust custom connection points intelligently when shape is resized` -> `Auto-anchor` -> `Force V/H` -> `3 line connector middle-line repositioning` -> `True dynamic connectors`
- `Shape text improvements` -> `Connector text`
- `Stable selection/hit testing` -> `Align selected shapes` -> `Regroup`
- `Most editing features` -> `Undo/redo`

## Recommended implementation order

### 1. Close document (canvas) prompts to save changes

Complexity: Low
Dependencies: Current document dirty-state tracking

Why here:
This is a contained document lifecycle fix that reduces data loss risk immediately and does not depend on rendering or geometry work.

High-level work:

- Track dirty state consistently per canvas.
- Prompt on close, app shutdown, and maybe canvas switch if applicable.
- Support `Save`, `Don't Save`, and `Cancel` paths cleanly.

### 2. Saving drawing updates MRU when a new filename is introduced

Complexity: Low
Dependencies: Current save flow and MRU list handling

Why here:
This is another low-risk document workflow item and pairs naturally with close/save behavior.

High-level work:

- Update MRU entries on first save and save-as.
- Avoid duplicate MRU entries for the same file.
- Keep multi-canvas save behavior aligned with the active document.

### 3. Add panels back in that have been removed: Toolbox, PropertyGrid, code editor

Complexity: Low to medium
Dependencies: Application shell layout and docking integration

Why here:
Several later features assume the shell surfaces exist. Restoring them early avoids planning work against missing UI.

High-level work:

- Reintroduce the removed panels into the docking layout.
- Restore panel commands, visibility toggles, and startup state.
- Verify panel-to-canvas communication still works.

### 4. Be able to select alternate shape when there is more than one option at the click point

Complexity: Medium
Dependencies: Current hit testing and selection model

Why here:
Selection ambiguity affects everyday editing and also informs later work on grouping, alignment, and connector manipulation.

High-level work:

- Detect multiple candidates at the click point.
- Decide on cycling, context menu, or modifier-based selection behavior.
- Keep grouped and nested shapes predictable.

### 5. Try intersection depth limit of 1 deep

Complexity: Medium
Dependencies: Current hit testing and grouping traversal

Why here:
This appears to be a targeted selection/hit-testing refinement and belongs with shape-picking behavior before more advanced layout operations.

High-level work:

- Make traversal depth explicit in hit-testing logic.
- Validate grouped and nested shape behavior.
- Ensure the new rule does not break connector or grip selection.

### 6. Scrollbars for canvas

Complexity: Medium
Dependencies: Current canvas bounds and viewport state

Why here:
Scrollbars establish a formal viewport model that multiple later features will reuse.

High-level work:

- Introduce a persistent viewport origin.
- Compute virtual extents from diagram bounds.
- Synchronize scrollbars with repaint and drag-based panning.

### 7. Zoom

Complexity: Medium
Dependencies: `Scrollbars for canvas`

Why here:
Zoom should be implemented on top of a real viewport abstraction, not beside it.

High-level work:

- Centralize world-to-screen and screen-to-world transforms.
- Scale rendering, hit testing, grips, and selection feedback consistently.
- Keep scrollbar ranges aligned with zoomed extents.

### 8. True drag-from-toolbox-onto-surface

Complexity: Medium
Dependencies: `Add panels back in`, `Scrollbars for canvas`, `Zoom`

Why here:
Real drag/drop needs a working toolbox and reliable coordinate transforms once the canvas is scrolled or zoomed.

High-level work:

- Replace click/place logic with real drag/drop.
- Translate drop points into diagram coordinates.
- Add insertion preview if practical.

### 9. Ruler margins / page boundaries

Complexity: Medium
Dependencies: `Zoom`

Why here:
Rulers and page bounds only make sense after viewport translation and scaling are stable.

High-level work:

- Define page size, origin, and margins.
- Render guides in viewport-aware coordinates.
- Keep them aligned under scrolling and zooming.

### 10. Printing

Complexity: Medium
Dependencies: `Ruler margins / page boundaries`

Why here:
Printing becomes more straightforward once page boundaries already exist.

High-level work:

- Reuse the diagram renderer for printer output where possible.
- Respect margins, scaling, and pagination.
- Keep printing semantics aligned with page boundaries.

### 11. Shape text improvements

Complexity: Medium
Dependencies: Existing shape rendering

Why here:
Text layout is localized enough to tackle before deeper connector work and it also unlocks better connector labeling later.

High-level work:

- Add alignment options beyond centered text.
- Constrain text to shape bounds.
- Support multiline layout and auto-wrap.
- Revisit text measurement for resize and redraw correctness.

### 12. Connector text

Complexity: Medium
Dependencies: `Shape text improvements`, existing connector rendering

Why here:
Connector labeling should reuse whatever text layout decisions are made for shapes where possible.

High-level work:

- Define label position and orientation rules.
- Attach text to connector geometry and persistence.
- Keep label placement stable during connector edits.

### 13. Other line caps besides an arrow and diamond

Complexity: Low to medium
Dependencies: Existing connector rendering only

Why here:
This is a relatively isolated connector rendering enhancement and can be folded into the basic connector polish phase.

High-level work:

- Extend the cap model.
- Update rendering and serialization.
- Expose cap selection in the editing UI.

### 14. Changing property (like end caps) forces a full page redraw

Complexity: Medium
Dependencies: Property change pipeline and invalidation model

Why here:
Once connector property editing expands, targeted invalidation becomes more valuable and easier to verify.

High-level work:

- Identify when full redraws are being triggered.
- Scope invalidation to affected shapes and connectors where possible.
- Preserve correctness for grouped and connected objects.

### 15. Custom defined connection points / custom anchor points

Complexity: Medium
Dependencies: Existing connector and shape metadata

Why here:
This starts the anchor/routing feature track and provides the model future connector behavior will build on.

High-level work:

- Add per-shape custom anchor definitions.
- Support editing, rendering, hit testing, and persistence.
- Include anchors on connectors if the model requires it.

### 16. Adjust custom connection points intelligently when shape is resized

Complexity: Medium to high
Dependencies: `Custom defined connection points / custom anchor points`

Why here:
Responsive anchor behavior is the next step after custom anchors exist.

High-level work:

- Decide whether anchor locations are absolute, relative, or rule-based.
- Recompute anchor positions during resize.
- Preserve user intent when possible.

### 17. Auto-anchor

Complexity: High
Dependencies: `Adjust custom connection points intelligently when shape is resized`

Why here:
Automatic anchor choice only makes sense once anchor definitions and resize behavior are stable.

High-level work:

- Define anchor scoring or nearest-side rules.
- Re-evaluate anchors when shapes move or resize.
- Keep behavior predictable during interactive dragging.

### 18. Force V/H (removes diagonal)

Complexity: High
Dependencies: `Auto-anchor`

Why here:
Orthogonal routing is easier to introduce after automatic anchor choice is available.

High-level work:

- Define orthogonal connector mode.
- Remove or prevent diagonal segments in routed connectors.
- Keep drag editing intuitive when only vertical/horizontal segments are allowed.

### 19. Need 3 line connector middle-line repositioning

Complexity: High
Dependencies: `Force V/H`

Why here:
Three-segment connector editing becomes meaningful once orthogonal routing exists.

High-level work:

- Represent the movable middle segment explicitly.
- Allow dragging the middle line without breaking endpoints.
- Keep rerouting stable after edits.

### 20. True dynamic connectors

Complexity: Very high
Dependencies: `Custom defined connection points / custom anchor points`, `Adjust custom connection points intelligently when shape is resized`, `Auto-anchor`, `Force V/H`, `Need 3 line connector middle-line repositioning`

Why here:
This is the culmination of the connector track and depends on stable anchor and routing behavior.

High-level work:

- Define dynamic rerouting semantics clearly.
- Recompute connector geometry when related shapes move or resize.
- Respect manual edits and anchor preferences.

### 21. Shape rotation

Complexity: High
Dependencies: Stable viewport math and shape geometry

Why here:
Rotation affects bounds, hit testing, selection handles, connector anchors, and text layout. It should wait until simpler coordinate work is already stable.

High-level work:

- Add rotation to shape state and persistence.
- Apply transforms consistently during rendering and selection.
- Rework bounds and hit testing for rotated shapes.

### 22. Snap shapes to centers and edges

Complexity: High
Dependencies: `Shape rotation`

Why here:
If rotation is planned, snapping should be built against the final geometry model rather than rewritten later.

High-level work:

- Define center and edge snap targets.
- Compute candidate guides from visible shapes.
- Render alignment hints during drag.

### 23. Align selected shapes left/top/right/bottom to leftmost/topmost/rightmost/bottommost

Complexity: Medium to high
Dependencies: `Be able to select alternate shape when there is more than one option at the click point`

Why here:
Batch alignment is simpler than full snapping, but it still depends on reliable selection semantics and bounds calculations.

High-level work:

- Define alignment commands against the selected set.
- Use consistent bounds for grouped and possibly rotated shapes.
- Keep undo granularity in mind for later history support.

### 24. "Regroup", so shapes can be manipulated and then regrouped

Complexity: High
Dependencies: Stable selection model, grouping behavior

Why here:
Regroup touches grouping semantics, parent/child relationships, and likely selection state. It is more invasive than basic alignment commands.

High-level work:

- Define how regroup remembers prior membership or grouping roots.
- Support temporary ungroup/edit/regroup workflows.
- Keep connectors and child ownership correct.

### 25. Better property UX

Complexity: High
Dependencies: `Add panels back in`, most feature properties should already exist

Why here:
The current PropertyGrid can surface earlier features while the object model settles. A better UX should come after the important editable behaviors are already in place.

High-level work:

- Identify the most common property-edit workflows.
- Group controls by user task rather than object internals.
- Design focused editors for anchors, text, line caps, and routing.

### 26. Undo/redo

Complexity: Very high
Dependencies: Broad coverage across the editing model

Why last:
Undo/redo is the broadest cross-cutting system in the backlog. Implementing it too early creates repeated extension work as more edit actions appear.

High-level work:

- Standardize edits as commands with forward and reverse actions.
- Cover creation, deletion, movement, resize, grouping, connector edits, and property changes.
- Keep replay behavior consistent with save/load and selection state.

## Suggested milestone grouping

### Milestone 1: Shell and document safety

1. Close document prompts to save changes
2. Saving drawing updates MRU when a new filename is introduced
3. Add panels back in that have been removed

### Milestone 2: Selection and viewport foundation

1. Select alternate shape at shared click point
2. Intersection depth limit of 1 deep
3. Scrollbars for canvas
4. Zoom
5. True drag-from-toolbox-onto-surface

### Milestone 3: Page and text workflow

1. Ruler margins / page boundaries
2. Printing
3. Shape text improvements
4. Connector text
5. Other line caps
6. Property-change redraw optimization

### Milestone 4: Anchors and routing

1. Custom anchor points
2. Resize-aware anchor adjustment
3. Auto-anchor
4. Force V/H connectors
5. 3 line connector middle-line repositioning
6. True dynamic connectors

### Milestone 5: Geometry and layout commands

1. Shape rotation
2. Snap shapes to centers and edges
3. Align selected shapes
4. Regroup

### Milestone 6: Cross-cutting UX and history

1. Better property UX
2. Undo/redo

## Manual regression suite

These are not new feature items. They should be rerun after the relevant milestones:

### Regression group A: Saving and document lifecycle

- Text alignment persists after save/reopen
- Saving with an unnamed second canvas does not throw
- Saving multiple canvases does not lose content

### Regression group B: Selection and grouping

- Multi-select remains additive and move works for the selected set
- Group move updates attached connectors exactly once
- Dropping into a group box auto-groups the child correctly
- Grouped copy/paste produces selectable and movable results

### Regression group C: Connector behavior

- Duplicate connector attachment does not cause double-move behavior
- Connector endpoints update once per move in grouped scenarios

## Completed items from `todo.txt`

These are already marked done and should stay out of the active backlog, but they belong in regression thinking:

- Connector added to shapes within a group box double-moves when the entire groupbox is moved
- Text shape top/bottom alignment was reversed
- Multi-select stopped working
- Saving when the second canvas had no filename threw an exception
- Saving multiple canvases lost content
- Copy/paste of a grouped shape produced an unselectable result
- Duplicate connector wiring caused double-move behavior
