# Surl — Task board

This folder is the `Tasks` shared project (`Tasks.shproj`), loaded by `Surl.slnx`.
It is the work queue for Claude Code: one Markdown file per task, and the folder a
file sits in *is* its status. There is no status field to keep in sync — move the
file and the status has changed.

## Folders

| Folder | Means |
| --- | --- |
| `Backlog/` | Defined and waiting. A task here is *ready* when it is assigned to Claude and every task it depends on is in `Done`. |
| `Doing/` | Claimed and in progress. Empty whenever no `/task-run` is active. |
| `Blocked/` | Wants to proceed but cannot. The latest `Log` entry names the blocker and who can clear it. |
| `Deferred/` | Deliberately set aside — not blocked, just not now. The latest `Log` entry says why. |
| `Done/` | Finished, with a completion date and a one-line note. |
| `Done/<yyyy-MM-dd_HHmm>/` | Archive. One folder per archive run, named for when it ran. Never edited. |

## Transitions

| From | May move to |
| --- | --- |
| `Backlog` | `Doing`, `Blocked`, `Deferred` |
| `Doing` | `Done`, `Blocked`, `Deferred`, `Backlog` |
| `Blocked` | `Backlog`, `Doing`, `Deferred` |
| `Deferred` | `Backlog` |
| `Done` | Nowhere except the archive. Reopening finished work is a new task. |

## Working the board from Claude Code

| Command | Does |
| --- | --- |
| `/task-plan <request>` | Breaks a request into tasks and files them in `Backlog`. Starts nothing. |
| `/task-run` | Claims the next ready task, delivers it through its pipeline, and files it as `Done` or `Blocked`. |
| `/task-run BL-###` | The same, for one named task. |
| `/task-run all` | Keeps taking the next ready task until none is ready or one ends `Blocked`. |
| `/task-status` | What is ready, what is waiting on what, and what is blocked and on whom. |
| `/task-archive [days]` | Moves tasks completed at least that many days ago (default 7; `0` for all) into a new timestamped folder under `Done/`. |

The rules, the task file format and the script behind these commands live in
`.claude/skills/task-board/`.

## Working the board by hand

Moving a file in Solution Explorer or File Explorer leaves the board valid, since the
folder is the status. The script does the same move but also validates the
transition and writes the `Log` line and completion date, so prefer it:

```
powershell -NoProfile -ExecutionPolicy Bypass -File .claude\skills\task-board\task-board.ps1 status
powershell -NoProfile -ExecutionPolicy Bypass -File .claude\skills\task-board\task-board.ps1 new -Title "Answer HEAD in Surl.Protocol.Http" -Pipeline feature
powershell -NoProfile -ExecutionPolicy Bypass -File .claude\skills\task-board\task-board.ps1 move -Id BL-007 -To Deferred -Reason "Not until Phase 2."
```

A task assigned to `Stewart` is a decision or action only Stewart can take. Claude
never claims one; the work that waits on the decision depends on it instead.
