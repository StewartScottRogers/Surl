---
id: BL-2017
title: Rewrite a renumbered task's own mentions of its old ID in task-board.ps1 dedupe
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2017 — Rewrite a renumbered task's own mentions of its old ID in task-board.ps1 dedupe

## Goal

After `task-board.ps1 dedupe` renumbers a task whose old ID was not shared, that task's own body names its new ID
wherever it named the old one.

## Context

Found while working BL-2003 (2026-11-27). In the `dedupe` command of `.claude/skills/task-board/task-board.ps1`, only
`id:` and the `#` heading of the renumbered file are rewritten: the loop over changed Markdown skips the task's own
file (`$file -eq $task.Path`). So BL-2003, filed as BL-1995 and renumbered in commit `b0c8a59d3`, kept an acceptance
criterion naming BL-1995, which by then was an unrelated task. `SKILL.md` says the old ID is rewritten in Markdown
changed since the ref. The rule for an ID an archived task holds, or one already at the ref, stays: those other
mentions cannot be told apart. The renumbering Log line must still name the old ID.

## Acceptance criteria

- [x] In a scratch copy of a board with two new tasks sharing an ID, `dedupe -Since <ref>` leaves no mention of the old ID in the renumbered task's file, and the task that keeps the ID is unchanged.
- [x] When an archived task holds the ID, or the renumbered file is already at the ref, the file's body mentions are left as they are, and its "Renumbered from" Log line names the old ID.
- [x] The `dedupe` row in `.claude/skills/task-board/SKILL.md` and the script's header say what is rewritten.

## Notes

Verified 2026-11-27 with a throwaway PowerShell script outside the repository, run under Windows PowerShell 5.1
against scratch git boards: 19 checks, 0 failures (two and three new duplicates, new beside shared, shared task moved
state folder, archived holder, earlier "Renumbered from" line). `dotnet build` and `dotnet format` clean; fast tests
30588 passed, 0 failed, 3 skipped.

Choices made while meeting the criteria:

- The pass over other changed files now skips every file that shared the ID. Before, with two new committed
  duplicates it also rewrote the keeper's `id:`, so both ended on the new ID (first criterion).
- "At the ref" is now one test for both cases (`Test-LiveAtRef`): the same path, or the same file name at a ref path
  that is gone here. A shared task that changed state folder therefore keeps its ID; the old exact-path test could
  renumber it and, with the new rewrite, change its body. A new task with the same file name as a shared one still
  counts as new, because the shared path is still present.
- "Renumbered from" Log lines are never rewritten: the Log is append-only and they record earlier IDs.
- When no archived task holds the ID, no "Renumbered from" line is added, so the renumbered file has no mention of the
  old ID (first criterion). The `dedupe` output and the factory's commit message still record old and new.
- No ADR: this corrects a board script to what `SKILL.md` already said it did.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. dedupe now rewrites a renumbered task's own mentions of its old ID and leaves the keeper unchanged; archived-holder bodies stay as they are.
