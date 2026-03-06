# FlowSharp Agent Notes

## Project Summary

- FlowSharp is a WinForms diagramming application targeting `.NET 8` on Windows.
- The main solution is `FlowSharp.sln`.
- The main desktop app entry point is `FlowSharp.csproj`.
- Core drawing and controller logic lives in `FlowSharpLib/`.
- Service-layer projects live under `Services/`.
- Automated tests live in `Tests/FlowSharp.Main.Tests` and `Tests/FlowSharp.Http.IntegrationTests`.
- Optional migration smoke checks live in `FS-HOPE/CodeTester`.
- `README.md` is the quick-start/build reference. `MIGRATION_SUMMARY.md` is the migration status reference.

## Communication Rules

- Write in plain, direct English.
- Do not end sentences with filler, taglines, or smug contrast phrases.
- Avoid repetitive endings such as:
  - "instead of guessing"
  - "rather than only talking about it"
  - "that will actually run on your machine"
  - "instead of being generic noise"
- Do not add motivational fluff or stock wrap-up language.
- If a short factual sentence is enough, stop there.

## Git And Commit Rules

- Do not push unless the user explicitly says to push.
- Before any commit, run `git status --short` and tell the user if the worktree is dirty.
- If the worktree is dirty and the commit will be partial, explicitly state:
  - which files will be included
  - which files will remain uncommitted
  - that the commit is only a subset of the current worktree
- If the user's request is ambiguous about whether to commit all changes or only the current task's changes, ask before committing.
- Do not silently commit a subset of a dirty worktree.
- After each commit, report the commit hash and commit message.

## Execution Rules

- Do what the user asked, but do not add extra side effects they did not request.
- `commit` does not mean `push`.
- `fix this` does not mean `commit everything`.
- If the repository state creates risk, say so before taking the action.
