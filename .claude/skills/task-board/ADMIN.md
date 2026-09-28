# Task board — administrative commands

`task-board.ps1` changes the board. `task-admin.ps1` only reads it.

That split is deliberate. The board's guarantee is that a task's status is the folder it
sits in and that every move is logged with a date, which holds only while exactly one
piece of code performs moves. Listing, searching and validating do not need that power,
so they do not get it: nothing in `task-admin.ps1` writes a byte to `Tasks/`.

| Want to | Use |
| --- | --- |
| Create, move, archive a task | `task-board.ps1` — see `SKILL.md`, which is binding |
| Look at the board without changing it | `task-admin.ps1`, below |

## Commands

```
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-admin.ps1 <command> [options]
```

| Command | Options | Does |
| --- | --- | --- |
| `list` | `-Folder`, `-Assignee`, `-Priority`, `-Pipeline`, `-Id`, `-NoArchive` | Tasks in one state folder, or all of them. `-Folder All` groups by state. Backlog rows carry the same ready / waiting / needs-Stewart marks and queue positions that `task-board.ps1 next` computes from. |
| `show` | `-Id` (required) | One task in full, preceded by what the file does not itself say: which dependencies are not yet `Done`, which tasks wait on this one, how many acceptance boxes are ticked, and its queue position if it is ready. |
| `find` | `-Text` (required), plus any `list` filter | Tasks whose title or body contains the text, with the matching line numbers. Case-insensitive substring. |
| `deps` | `-Id` (required) | What the task waits on and what waits on it, both transitively, each with its current state. Names a dependency that is not on the board. |
| `check` | `-FailOnProblem` | Validates the whole board. See below. |
| `report` | | Counts by state, assignee, pipeline and priority; what is ready; what waits on Stewart; completions by date. |

Slash commands wrap the four used most: `/task-list`, `/task-show`, `/task-find`,
`/task-check`. `deps` and `report` are called directly.

### What `check` validates

Duplicate IDs, and an `id` that disagrees with its file name. A missing `title`. Unknown
`priority`, `assignee` or `pipeline`. A missing `requirement` — write `none` rather than
leaving it blank. A `created` that is not `yyyy-MM-dd`. A dependency that is not on the
board, and a task depending on itself. Dependency cycles, reported as the path round the
loop. A `Done` task with an unticked acceptance box or a malformed completion date. An
unfinished task carrying a completion date. A `Doing` task whose dependencies are not
`Done`. More than one task in `Doing`. A missing `## Goal`, `## Context`,
`## Acceptance criteria`, `## Notes` or `## Log`. Content after `## Log`, which must be
last. An unfinished task with no acceptance criteria, since nothing about it is then
verifiable. Gaps in the ID sequence, because IDs are never reused.

`-FailOnProblem` makes it exit 1, so it can run as a hook or a build step.

**A finding is not automatically a task to fix.** It is either a task that needs editing
or a rule that needs changing. Deciding which is the point. Two rules were removed from
this checker after they flagged every task on the board and meant nothing:

- **Non-ASCII characters in task files.** The ASCII-only rule belongs to the `.ps1`
  files, because Windows PowerShell 5.1 reads a script without a byte-order mark in the
  system code page and would corrupt a non-ASCII literal. Task files are UTF-8, and every
  task heading legitimately carries an em dash.
- **Acceptance criteria on finished tasks.** A task already in `Done` cannot be improved
  by adding checkboxes to it. The rule now applies only while the work is still ahead.

## Adding a command

Three edits, all in `task-admin.ps1`:

1. Add the name to the `ValidateSet` on `$Command`.
2. Write `function Invoke-<Name>([object[]] $Tasks)`. Every task is already parsed —
   `Get-Tasks` gives you `Id`, `Number`, `Title`, `Priority`, `Assignee`, `Pipeline`,
   `Requirement`, `DependsOn`, `Created`, `Completed`, `State`, `Archived`, `BoxesTotal`,
   `BoxesOpen`, `HasHeader`, `Path`, `Relative` and the full `Text`.
3. Add one row to the `$Verbs` table at the foot of the file.

Add a parameter to the `param` block only if an existing one does not already fit, and
give it a `ValidateSet` when its values are a closed set, so a typo fails at the call
rather than silently matching nothing.

Reuse the helpers rather than reimplementing them, and in particular use
`Get-ReadyTasks` for anything about readiness: it is a deliberate copy of
`task-board.ps1`'s definition, so the two can never disagree about which task is
takeable. If you change what ready means, change it in both.

Keep the file ASCII only, and keep it read-only. A command that needs to change a task
belongs in `task-board.ps1`, where the transition table and the log live.

## Conventions a new command should follow

- Print a header line with a count, then indented rows. `report` and `list` show the
  shape.
- Name a task as `BL-###  <state>  <title>`, in that order — the state is usually what
  the reader is deciding from.
- Say `(none)` rather than printing nothing, so an empty result is distinguishable from a
  command that failed.
- Do not re-sort rows that carry a queue position.
- Resolve the repository root the way the two scripts already do: `CLAUDE_PROJECT_DIR`
  when it is set, otherwise three levels up from `$PSScriptRoot`.
