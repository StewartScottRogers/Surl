---
id: BL-1778
title: Stop the task board giving a live task the ID of an archived task
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board]
requirement: none
created: 2026-11-25
completed: 2026-11-26
---
# BL-1778 — Stop the task board giving a live task the ID of an archived task

## Goal

The dedupe command renumbers a live task that holds the ID of an archived task.

## Context

- On 2026-11-25 two tasks held BL-1773: one archived in Tasks/Done/2026-11-25_2312, one filed by another lane and
  later claimed. The dedupe command of task-board.ps1 groups only tasks that are not archived, so it never saw the pair.
- The archived file is never edited; the live one takes the next free ID. Get-DoneIds counts the archived one, so a
  task depending on the live one would look ready.

## Acceptance criteria

- [x] dedupe -Since <ref>, run on a copy of the board with one archived and one live task sharing an ID, renumbers the live one only; the task notes the command and its output.
- [x] SKILL.md's dedupe row says what it does with an archived duplicate.

## Notes

Run on 2026-11-26 on a copy of `Tasks` in a temporary git repository (`CLAUDE_PROJECT_DIR` pointing at it):
`powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1 dedupe -Since <ref>`

- Live task already at the ref (the real BL-1773 pair, plus a test task BL-1900 depending on BL-1769 and BL-1773),
  `-Since HEAD`, duplicate-ID warnings omitted:
  ```
  BL-1773 -> BL-1901  Withhold secrets from HTTP/3 decoded header text whatever the field order and whatever bytes a value holds
  BL-1900 depends-on: BL-1773 -> BL-1901
  ```
  Every archived file's hash was unchanged; a second run printed `No duplicate IDs.`
- Live task not at the ref (a new BL-1769 and a new task depending on it, plus a changed archived file mentioning
  BL-1769), `-Since base-b`:
  ```
  BL-1769 -> BL-1951  Test task BL-1769
  ```
  Archived files unchanged; the new dependent now reads `depends-on: [BL-1951]`; BL-1900, at the ref, kept BL-1769.
- Two new live tasks sharing BL-1960, none archived: one renumbered to BL-1961, as before.

Choices:
- A live duplicate of an archived ID is renumbered even when it is already at the ref, because the archived file is
  never edited.
- For one already at the ref, only open tasks' `depends-on` entries are rewritten: a text mention may mean either
  task. A dependency that really meant the archived task then waits on the live one, which is later than needed but
  never early. The renumbered task's Log names its old ID, so old mentions can be traced.
- The rewrite of this lane's changed files now skips archived task files.
- The real board still holds the live BL-1773 in `Tasks/Done`. It is outside this task's `touches`; the next lane
  integration runs `dedupe` and renumbers it.
- `verify`: build 0 warnings, format clean, fast tests 29804 passed, 3 skipped, 0 failed.

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. dedupe renumbers a live task that holds an archived task's ID and leaves the archived file untouched.
