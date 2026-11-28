---
id: BL-1985
title: Retry the next shift a -Continuous handover starts when it refuses because a lane is still running
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, Documentation/Wiki/Dark-Factory-Recipes.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1985 — Retry the next shift a -Continuous handover starts when it refuses because a lane is still running

## Goal

A `-Continuous` shift that ends with work still ready always leads to a running next shift,
even when one of its lanes is still finishing a task at the handover.

## Context

Shift `20261126-201542` (`Z:\repos\Surl.logs`): at 06:46:45 its coordinator logged "work is
still ready; next shift started (herdr tab w14:t1T)" (`RunDarkFactory.ps1`, near line 3417)
and closed its own tab. At 06:46:47 the new shift `20261127-064646` logged "refuse  lane 6 of
shift 20261126-201542 is still running BL-1815" (near line 3075) and exited 1. Lane 6 finished
BL-1815 at 06:56:56. Nothing retried, so no shift ran from 06:56 to 13:06, and the frozen merge
PR #159, green on every platform, waited six hours for a coordinator to merge it.

When a restart was tried at 13:06, the new shift also refused with "factory/phase-1 differs
from origin/factory/phase-1; push or pull first" because the main checkout was one commit
behind (only behind, with a clean tree). A fast-forward-only pull fixed it by hand.

Fix direction, for the implementer to settle: the handover waits for every lane of the ending
shift to stop before it starts the next one (or the next shift waits on the refusing lanes
rather than exiting), and a checkout that is only behind its remote with a clean working tree
is fast-forwarded rather than refused. Keep the refusal for a dirty tree or a branch that is
ahead or has diverged.

## Acceptance criteria

- [x] When a `-Continuous` shift ends while one of its lanes still holds a task, the next shift
      starts once that lane stops, shown by a trace in this task's Notes (a short `-Hours` run
      or a `-DryRun` that simulates the held lane).
- [x] The next shift's trace logs a `shift   start` line; it never ends with only a `refuse`
      line for a lane of the shift it replaces.
- [x] A start in a checkout that is only behind its remote, with a clean working tree,
      fast-forwards and starts; a dirty or ahead or diverged checkout is still refused.
- [x] `Documentation/Wiki/Dark-Factory-Recipes.md` and the script header describe the
      handover as it now behaves.

## Notes

Cause: shift `20261126-201542` ran 20:16 to 04:16, so its coordinator's give-up time
(`-Hours` + `-TaskMinutes` + 30) was 06:46. It left its wait loop at that time while lane 6,
alive, still held BL-1815, and handed over. (Lane 6 had been added with a full 8 hours, which
BL-1917 has since fixed; a held task can still outlast the give-up time.)

Choices:

- The handover waits, rather than the next shift: the ending coordinator is the one that
  restarts dead lanes and merges frozen pull requests, and the next shift's refusal stays as a
  guard against a duplicate manual start.
- Past the give-up time `Get-OverdueLaneStep` answers per unfinished lane: `wait` for a
  running one heard from (start or heartbeat) in the last 60 minutes, `stop` for a running one
  silent longer, `leave` for a dead or never-started one. Without the 60-minute rule one hung
  lane would stop the factory for good. 60 minutes is well past the longest healthy silence,
  an integration's build and two test runs. A stopped lane is restarted like any dead lane, or
  adopted by the next shift once its five restarts are used.
- `Sync-ShiftCheckout` (fast-forward when only behind) applies to a lane shift's start, which
  is where the refusal was. The single runner never refused a differing branch and is unchanged.

Rehearsals, all passing: `-TestShiftEnd` (five new overdue-lane cases) and `-TestShiftBranch`
(level, behind, dirty, ahead, diverged).

End-to-end trace, 2026-11-27: a real coordinator and real lanes of a copy of the script on a
throwaway repository with fake `claude`, `dotnet` and `gh`, run with `-Lanes 2 -Hours 0.01
-Continuous`, its checkout one commit behind. The copy differs only in that the give-up time is
the shift's end and its windows are hidden.

```
17:02:47 -      branch  factory/rehearsal was behind origin/factory/rehearsal; fast-forwarded to bd34e9dc5
17:02:48 -      shift   start  2 lanes  branch=factory/rehearsal model=sonnet until 17:03
17:03:26 -      shift   past its end; waiting for lane 1 BL-001, lane 2 BL-002 to stop
17:04:33 -      lane    lane 1 ended (time up): done=1 blocked=0 requeued=0 stalled=0
17:04:33 -      lane    lane 2 ended (time up): done=1 blocked=0 requeued=0 stalled=0
17:04:33 -      shift   end  2 lanes
17:04:38 -      shift   work is still ready; next shift started (pid 43888)
--- next shift, DarkFactory-20261127-170439.log
17:04:40 -      shift   start  2 lanes  branch=factory/rehearsal model=sonnet until 17:05
```

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. A -Continuous handover now waits for every running lane before starting the next shift, and a lane shift fast-forwards a checkout that is only behind
