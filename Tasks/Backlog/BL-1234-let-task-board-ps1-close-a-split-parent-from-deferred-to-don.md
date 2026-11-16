---
id: BL-1234
title: Let task-board.ps1 close a split parent from Deferred to Done once every part it names is Done
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board]
requirement: none
created: 2026-11-15
completed:
---
# BL-1234 — Let task-board.ps1 close a split parent from Deferred to Done once every part it names is Done

## Goal

`task-board.ps1 close-split -Id <parent>` moves a Deferred task whose work was split into other tasks
straight to `Done` once every one of those parts is in `Done` or a `Done` archive, and refuses otherwise,
so finished split parents stop sitting in `Deferred` as if nobody wanted them.

## Context

- Today the transition table in `.claude/skills/task-board/task-board.ps1` (`$AllowedMoves`, line 110)
  allows `Deferred` to move to `Backlog` only, and `move -To Done` refuses any task with an unticked box
  (line 433). The dark factory defers every parent it splits (`RunDarkFactory.ps1` line 2053: "Move {ID}
  to Deferred with task-board.ps1 move -Reason "Split into BL-a, BL-b, ..."), so on 2026-11-15 the
  `Deferred` folder held 27 tasks whose work was all done by their parts (review by `task-planner`,
  2026-11-15). Moving them through `Backlog` and `Doing` would let a lane claim and redo the work.
- Design, decided here by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"); it changes
  board tooling, not Surl's behaviour, so no ADR:
  - New command `close-split` with `-Id` (required) and `-Parts BL-a,BL-b` (optional).
  - Without `-Parts`, the parts are every `BL-###` on the parent's **last** Log line containing
    `Split into` (forms on the board today: `Split into BL-875, BL-876, BL-877, BL-878, BL-879 (timed
    out ...)`, `Split into BL-1023, BL-1024 and BL-1025 (over ...)`). With `-Parts`, those IDs are the
    parts (for a parent finished by other later work rather than a split, e.g. BL-1084 by BL-1121).
  - Refuses, with a message naming the reason: the task is not in `Deferred`; no parts were found and none
    given; a part is not on the board; a part is not in `Done` or an archive (name each one); a part is
    the parent itself.
  - On success: ticks every `- [ ]` box, sets `completed`, appends the Log line
    `- <today>: Deferred -> Done. Closed by its parts, all Done: BL-a, BL-b, ...`, and moves the file to
    `Tasks/Done/`, all with the same helpers `move` uses (`Read-Text`, `Set-FrontMatterField`,
    `Add-LogLine`, `Write-Text`).
  - `move -Id <deferred task> -To Done` stays refused; its message adds "use close-split for a split
    parent".
- Keep `task-board.ps1` ASCII only (Windows PowerShell 5.1 reads it in the system code page;
  `ADMIN.md`). `task-admin.ps1` needs no change: its `check` flags unticked boxes on a `Done` task, which
  `close-split` leaves none of.
- Test against a scratch board, never the real one: the script resolves the repository from
  `CLAUDE_PROJECT_DIR` when it is set, so copy `.claude/skills/task-board` and a few sample task files into
  a temporary folder with `Tasks/Backlog`, `Tasks/Doing`, `Tasks/Blocked`, `Tasks/Deferred` and
  `Tasks/Done`, and point `CLAUDE_PROJECT_DIR` at it.

## Acceptance criteria

- [ ] `.claude/skills/task-board/task-board.ps1` has the `close-split` command as described in Context,
      and `$AllowedMoves` still lists only `Backlog` for `Deferred`.
- [ ] On a scratch board (Context), `close-split` on a Deferred parent whose `Split into` parts are all in
      `Done` moves it to `Done` with every box ticked, `completed` set and the Log line above; Notes record
      the commands run and their output.
- [ ] On the same scratch board, `close-split` refuses a parent with one part still in `Backlog` (naming
      that part), a parent not in `Deferred`, and a parent with no `Split into` line and no `-Parts`; and
      `close-split -Parts` closes a parent with no `Split into` line. Notes record each refusal's message.
- [ ] `.claude/skills/task-board/SKILL.md` lists `close-split` in the script's command table, and its
      States table says `Deferred` moves to `Backlog`, or to `Done` through `close-split` only.
- [ ] `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1 status`
      on the real board still runs, and `task-board.ps1` contains no non-ASCII byte.

## Notes

- Filed by `task-planner` on 2026-11-15 while reviewing the 27 Deferred tasks; BL-1235 uses the command
  to close them.

## Log

- 2026-11-15: Created.
