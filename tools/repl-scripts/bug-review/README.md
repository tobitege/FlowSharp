# Bug Review REPL Scripts

These macro files mirror the seven `Verify completed bug fixes:` flows in [todo.txt](/c:/github/FlowSharp/todo.txt), plus runtime endpoint coverage for completed feature surfaces.

Usage from the REPL:

```text
:load C:\github\FlowSharp\tools\repl-scripts\bug-review\01-text-alignment.flow
```

Notes:

- The scripts use `%TEMP%` for saved diagrams and PNGs.
- `runmacro` now expands environment variables in file paths, so the scripts can be loaded directly.
- The scripts are written for a fresh FlowSharp session launched with `FlowSharpRuntimeControlModules.xml`.
- `08-runtime-feature-surfaces.flow` exercises durable runtime commands for undoable property edits, connector labels and line caps, connector conversion/removal, align, snapped drag, regroup, custom connection points, dynamic rerouting, focus/pan observation, persistence, and print-page rendering.
