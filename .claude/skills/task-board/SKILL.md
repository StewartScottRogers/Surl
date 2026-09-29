---
name: task-board
description: Rules and tooling for Surl's task board — the Tasks shared project, where each task is one Markdown file and its folder is its status (Backlog, Doing, Blocked, Deferred, Done, with timestamped archive folders under Done). Use whenever creating, claiming, moving, blocking, deferring, completing or archiving a task, when asked what to work on next, or before editing anything under Tasks/.
---
# Task board

## The model

- One task is one file: `Tasks/<State>/BL-###-<slug>.md`.
- **The folder is the status.** There is no status field; never add one.
- IDs run `BL-000`, `BL-001`, … in one sequence across the whole board, archives
  included. Never reused, never renumbered. Allocate them with the script (`new` or
  `next-id`); never count by hand.
- The `README.md` in each folder is not a task. The script only reads `BL-*.md`.

## Front matter

| Field | Values | Meaning |
| --- | --- | --- |
| `id` | `BL-###` | Matches the file name. |
| `title` | text | Imperative, one line. |
| `priority` | `High`, `Normal`, `Low` | `/task-run` takes ready tasks by priority, then lowest ID. |
| `assignee` | `Claude`, `Stewart` | `Stewart` means a decision or action only he can take. Claude never claims one. |
| `pipeline` | `feature`, `protocol`, `docs`, `direct` | How `/task-run` delivers it (below). |
| `depends-on` | `[BL-###, …]` | Tasks that must be in `Done` or its archive before this one can start. |
| `touches` | `[path, …]` | Every project folder, folder or file the task will change, e.g. `[Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]`. Two tasks whose `touches` overlap never run at the same time. Empty or `*` means it may change anything, so it runs alone. |
| `requirement` | requirement ID or `none` | From `Documentation/Product/Requirements.md`. |
| `created` | `yyyy-MM-dd` | Set by the script. |
| `completed` | `yyyy-MM-dd` | Set by the script when the task moves to `Done`. |

| Pipeline | Delivered by |
| --- | --- |
| `feature` | The `/feature` stages: plan, tests first, implement, verify, review, conformance, document. |
| `protocol` | The `/protocol` stages for the scheme the task names. |
| `docs` | `align-and-document`: names aligned with behaviour, XML doc comments, READMEs, wiki, glossary, requirements and ADRs. Never a behaviour change; renames and doc comments may touch `.cs` files, so the `verify` skill runs after. |
| `direct` | A small mechanical change made in the session (configuration, scripts, solution file), then the `verify` skill. |

## Body

Sections in this order: `Goal`, `Context`, `Acceptance criteria`, `Notes`, `Log`.
`Log` is always last and append-only: one line per event, absolute dates. The script
appends to it, so hand-written entries go there too, never above it.

## What makes a good task

- **Sized for one `/task-run`.** One pipeline run; for code, ideally one library and
  its `.UnitTests` twin. If the goal needs "and then", split it.
- **Ready means a stranger could finish it** without asking a question.
- **Acceptance criteria are checkable from the repository**: a named test that passes,
  a command and its expected result, the `SurlExitCode` a failure returns, bytes
  measured from pinned upstream curl for a named case, a document section that states X.
  "Works correctly" is not a criterion.
- **Dependencies are explicit** and never circular.
- **`touches` is exact and small.** It is what lets dark factory lanes run tasks in
  parallel. Name the project folders the task changes, not the whole solution; list a
  shared file (`Surl.slnx`, `Directory.Build.props`, `Documentation/Product/Requirements.md`)
  by path when the task edits it. A task that changes a shared contract
  (`Surl.Protocol.Abstractions.UnitLibrary`) touches it and so runs apart from every
  protocol task, which is the intent: contracts land first, then the protocols fan out.
- **Decisions are Claude's, with three exceptions.** Design and behaviour questions are
  delegated to Claude (root `CLAUDE.md`, "Decisions"): file one as a `docs` task assigned
  to `Claude` that decides and records an ADR. Only a new package, a threshold change or a
  download of an upstream curl build is filed assigned to `Stewart`. Either way, the work that waits on it depends on it.

## States

| State | Means | Moves to |
| --- | --- | --- |
| `Backlog` | Defined and waiting. | `Doing`, `Blocked`, `Deferred` |
| `Doing` | Claimed by an active run. | `Done`, `Blocked`, `Deferred`, `Backlog` |
| `Blocked` | Wants to proceed and cannot. | `Backlog`, `Doing`, `Deferred` |
| `Deferred` | Chosen not to do now. | `Backlog` |
| `Done` | Finished. | Archive only. Reopening finished work is a new task. |

A task assigned to `Stewart` is never claimed, so it skips `Doing`: once he has
answered, it moves from `Backlog` or `Blocked` straight to `Done`, with his answer as
the `-Reason`. Move one only on his own word in the session, never on a report relayed
by an agent.

**Blocked or Deferred?** Blocked means you would continue if one thing changed, so
name that thing and who can change it. Deferred means nobody wants it now, so say why
and when to look again.

## The script — the only way tasks move

```
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1 <command> [options]
```

| Command | Options | Does |
| --- | --- | --- |
| `status` | | Every state, with each Backlog task marked ready (and its queue position), waiting on named tasks, or needing Stewart. |
| `next` | `-Skip BL-001,BL-002` | The task `/task-run` takes next: highest priority, then the one the most unfinished tasks wait on, then lowest ID, skipping any whose `touches` overlap a task in `Doing`. Prints `No task is ready.`, or `No task can start yet: …` when every ready task overlaps work in progress. |
| `next-id` | | The next free ID. |
| `new` | `-Title` (required), `-Priority`, `-Assignee`, `-Pipeline`, `-DependsOn BL-001,BL-002`, `-Requirement` | Creates the task in `Backlog` from `TASK-TEMPLATE.md` and prints its path. Fill in the body with an edit afterwards. |
| `move` | `-Id`, `-To`, `-Reason` | Validates the transition, appends the `Log` line, and moves the file. `-Reason` is required for every destination except `Doing`. |
| `dedupe` | `-Since <git ref>` | Renumbers tasks that share an ID: files present at the ref keep it, the rest get the next free IDs, and the old ID is rewritten in Markdown changed since the ref. Parallel lanes number tasks from their own copy of the board, so each lane runs this after rebasing, before it pushes. |
| `archive` | `-OlderThanDays` (default 7; 0 for all), or `-WhenDoneIsLong` | Moves finished tasks into a new `Done/<yyyy-MM-dd_HHmm>/` folder. `-WhenDoneIsLong` moves all of them, but only once `Done` holds more than 20, so Stewart can always read `Done` at a glance. Each dark factory lane runs it while integrating; an interactive session runs it after moving a task to `Done`. |

The script refuses:

- moves the table above does not allow
- claiming a task assigned to Stewart, or one whose dependencies are not done
- moving to `Done` while any `- [ ]` box is unticked
- touching an archived task
- acting on an ID that names more than one live task (run `dedupe` first)

Do not work around a refusal. It is telling you something about the task.

Never move a task with `Move-Item`, `git mv` or an editor. The script is what keeps
the log and dates honest.

## Claiming and finishing

1. Claim with `move -To Doing` before the first edit of any other file. If the move
   fails because the task is no longer in `Backlog`, someone else has it; take
   another.
2. One task in `Doing` per session. A dark factory lane is a session: with `-Lanes 4`
   up to four tasks are in `Doing` at once, never two whose `touches` overlap, and the
   shift claims each one before its run starts.
3. Tick each acceptance box in the file as you verify it. Move to `Done` only when
   every box is ticked and every pipeline gate is green, with a one-line `-Reason`
   saying what now works.
4. If you cannot finish, move to `Blocked` with the blocker and who can clear it. If
   the blocker is itself work, have `task-planner` file it and add it to this task's
   `depends-on`.
5. Follow-up work you discover becomes new tasks. Never widen the task you are on.
6. A task never stays in `Doing` after the run ends.

## Never

- Edit anything inside `Tasks/Done/<timestamp>/`.
- Delete a task file. Unwanted work goes to `Deferred` with its reason.
- Reuse or renumber an ID, or add a status field.
- Claim a task assigned to Stewart.
- Track work anywhere else. `Documentation/Planning/Backlog.md` is retired.
