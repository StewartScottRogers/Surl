---
id: BL-1235
title: Close the 27 finished Deferred parents to Done with the split-parent close
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1234]
touches: [Tasks/Deferred, Tasks/Done]
requirement: none
created: 2026-11-15
completed:
---
# BL-1235 — Close the 27 finished Deferred parents to Done with the split-parent close

## Goal

`Tasks/Deferred` holds none of the 27 tasks listed below: each is in `Tasks/Done` with a Log line naming the
tasks that did its work, moved by BL-1234's `close-split` command.

## Context

- `task-planner` reviewed every Deferred task on 2026-11-15 against the code and the `Done` archives. All 27
  are finished: 26 were split and every part is Done, and BL-1084 was done by BL-1121. Each carries a
  2026-11-15 Log line saying so. None of them is to be redone.
- Run from the repository root, one command per row, with
  `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1`:

| Parent | Command arguments | Parts (all Done) |
| --- | --- | --- |
| BL-395 | `close-split -Id BL-395` | BL-875, BL-876, BL-877, BL-878, BL-879 |
| BL-427 | `close-split -Id BL-427` | BL-1023, BL-1024, BL-1025 |
| BL-433 | `close-split -Id BL-433` | BL-1026, BL-1027 |
| BL-439 | `close-split -Id BL-439` | BL-1028, BL-971 |
| BL-445 | `close-split -Id BL-445` | BL-1094, BL-1095, BL-1093 |
| BL-468 | `close-split -Id BL-468` | BL-978, BL-979 |
| BL-485 | `close-split -Id BL-485` | BL-1032, BL-1017, BL-1018, BL-1019 |
| BL-501 | `close-split -Id BL-501` | BL-975, BL-976 |
| BL-522 | `close-split -Id BL-522` | BL-973, BL-974 |
| BL-545 | `close-split -Id BL-545` | BL-1005, BL-1006, BL-1007, BL-1029, BL-1008, BL-1009, BL-1010, BL-1030 |
| BL-555 | `close-split -Id BL-555` | BL-1002, BL-1003, BL-1004 |
| BL-595 | `close-split -Id BL-595` | BL-938, BL-939, BL-940, BL-941 |
| BL-597 | `close-split -Id BL-597` | BL-942, BL-934, BL-935, BL-936, BL-937 |
| BL-653 | `close-split -Id BL-653` | BL-880, BL-881, BL-882, BL-883 |
| BL-654 | `close-split -Id BL-654` | BL-993, BL-994, BL-995 |
| BL-663 | `close-split -Id BL-663` | BL-987, BL-988, BL-989 |
| BL-666 | `close-split -Id BL-666` | BL-990, BL-991, BL-992 |
| BL-816 | `close-split -Id BL-816` | BL-1016, BL-1022, BL-972, BL-977 |
| BL-833 | `close-split -Id BL-833` | BL-980, BL-981, BL-982, BL-983 |
| BL-834 | `close-split -Id BL-834` | BL-1011, BL-1012, BL-1013, BL-1014, BL-1031, BL-1015 |
| BL-836 | `close-split -Id BL-836` | BL-984, BL-985, BL-986 |
| BL-850 | `close-split -Id BL-850` | BL-999, BL-1000, BL-1001 |
| BL-852 | `close-split -Id BL-852` | BL-996, BL-997, BL-998 |
| BL-860 | `close-split -Id BL-860` | BL-1020, BL-1021 |
| BL-1051 | `close-split -Id BL-1051` | BL-1097, BL-1098, BL-1099 |
| BL-1084 | `close-split -Id BL-1084 -Parts BL-1121` | BL-1121 (no split; see BL-1084's Notes) |
| BL-1109 | `close-split -Id BL-1109` | BL-1111, BL-1112 |

- If `close-split` refuses a row, do not work around it: record the refusal in Notes and leave that parent
  in `Deferred`.
- After the moves, run `task-board.ps1 archive -WhenDoneIsLong` as an interactive session does after moving
  a task to `Done` (`SKILL.md`).

## Acceptance criteria

- [ ] Each of the 27 commands above has run, and its output line (`<id>  Deferred -> Done  ...`) is in Notes.
- [ ] `task-board.ps1 status` shows `Deferred (0)`, or Notes name each parent left there and the refusal
      that kept it.
- [ ] `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-admin.ps1 check`
      reports no problem in any of the 27 moved files.

## Notes

- Filed by `task-planner` on 2026-11-15. No code changes; `Tasks/` only.

## Log

- 2026-11-15: Created.
