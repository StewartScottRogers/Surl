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
completed:
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

- [ ] In a scratch copy of a board with two new tasks sharing an ID, `dedupe -Since <ref>` leaves no mention of the old ID in the renumbered task's file, and the task that keeps the ID is unchanged.
- [ ] When an archived task holds the ID, or the renumbered file is already at the ref, the file's body mentions are left as they are, and its "Renumbered from" Log line names the old ID.
- [ ] The `dedupe` row in `.claude/skills/task-board/SKILL.md` and the script's header say what is rewritten.

## Notes

## Log

- 2026-11-27: Created.
