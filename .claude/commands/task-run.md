---
description: Claim the next ready task (or a named one) from the Tasks board, deliver it through its pipeline, and file it as Done or Blocked.
argument-hint: [BL-### | all] — empty takes the next ready task; all keeps going until none is ready
---
Work the task board. Argument: **$ARGUMENTS**

Read `.claude/skills/task-board/SKILL.md` first; its rules are binding. `$TB` below
means
`powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1`.

## 1. Select
- **A task ID was given:** use it. The script will refuse the claim if the task is
  assigned to Stewart, waiting on dependencies, or in a state that cannot move to
  `Doing`; report its reason and stop.
- **Empty or `all`:** `$TB next`. If it prints `No task is ready.`, run `$TB status`,
  report why nothing is ready (waiting on dependencies, waiting on Stewart, or
  blocked, and on what), and stop.

## 2. Claim
If the task is already in `Doing` because a dark factory lane claimed it for this run,
skip the move. Otherwise `$TB move -Id <ID> -To Doing` before touching any other file. Then read the task file
at its new path. It is the specification: its `Goal`, `Context` and
`Acceptance criteria` are what "done" means.

## 3. Deliver, by the task's `pipeline`
- `feature`: run the `/feature` stages, with the task file's `Goal`, `Context` and
  `Acceptance criteria` as the feature text, given verbatim to `protocol-architect`.
  The acceptance criteria are gates in addition to each stage's own gate.
- `protocol`: run the `/protocol` stages for the scheme the task names, on the same
  terms.
- `docs`: delegate to `align-and-document` with the task file, then invoke the `verify`
  skill if it touched any `.cs` or project file.
- `direct`: make the change yourself, then invoke the `verify` skill.

As you go, record the plan's summary and anything learned under the task's `Notes`,
and tick each acceptance box as you verify it.

## 4. File the outcome
A task never stays in `Doing` when you finish. It ends in one of these:
- **Finished.** Every box is ticked and every gate is green:
  `$TB move -Id <ID> -To Done -Reason "<one line: what now works>"`.
- **Cannot finish.** A gate cannot be passed without a decision, an approval, an ADR,
  or work outside this task:
  `$TB move -Id <ID> -To Blocked -Reason "<what blocks it, and who can clear it>"`.
  If the blocker is itself work, have `task-planner` file it first, then add its ID to
  this task's `depends-on`.
- **Follow-up work found** (an option still unimplemented, an edge case set aside):
  have `task-planner` file it as new tasks. Never widen the current task to absorb it.

## 5. Repeat or report
With `all`, go back to step 1 until nothing is ready or a task ends `Blocked`. Stop at
the first `Blocked` so the user sees it before more work piles on top.

For each task, report its ID, its outcome, what now works, the projects touched, test
counts, and the IDs of any tasks filed. Do not commit unless the user asks.
