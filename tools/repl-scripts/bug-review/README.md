# Bug Review REPL Scripts

These macro files mirror the seven `Verify completed bug fixes:` flows in [todo.txt](/c:/github/FlowSharp/todo.txt).

Usage from the REPL:

```text
:load C:\github\FlowSharp\tools\repl-scripts\bug-review\01-text-alignment.flow
```

Notes:

- The scripts use `%TEMP%` for saved diagrams and PNGs.
- `runmacro` now expands environment variables in file paths, so the scripts can be loaded directly.
- The scripts are written for a fresh FlowSharp session with the runtime-control services enabled.
