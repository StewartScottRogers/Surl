# ADR-0047 — `-Lanes Auto` paces dark factory lanes to the usage windows, the board and the machine

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29.
  Stewart asked for Surl's dark factory to do everything the Curl port's dark factory does
  for lane work; the Curl port decided this design in its ADR-0130 and its amendments, and
  Surl takes it whole.

## Context

Surl's `RunDarkFactory.ps1` was copied from the Curl port on 2026-09-28 (ADR-0002), before
the Curl port taught its factory to size itself. A fixed `-Lanes N` is right for one Claude
plan and wrong for the next: too few lanes leave tokens unspent, too many hit the 85%
five-hour stop early and idle until the reset. Stewart moves between plans, so the factory
should measure what the current one sustains rather than be retuned.

The factory is tooling, not a Surl behaviour: no byte upstream curl sends or reads depends
on it, so ADR-0003's oracle rule does not apply, and copying the Curl port's script is
copying code, which ADR-0003 allows.

`Get-UsageReading` already reads the `five_hour` and `seven_day` utilization and their
`resetsAt` from each run's `rate_limit_event`. Utilization is a fraction of the current
plan, in whole hundredths, so the factory measures how fast one lane spends the plan and
sizes from that without knowing the tier.

## Decision

1. **`-Lanes` accepts `Auto` or a whole number 1 to 16.** A number keeps the fixed
   behaviour. `Auto` always runs the coordinator with lane worktrees, even at one lane.
   `-MaxLanes` (default 6, range 1 to 16) is Auto's lane maximum.

2. **Burn-rate meter.** Every 15 minutes (the *step*), except while the shift waits for a
   session, the coordinator records a sample: the time, the five-hour and weekly
   utilization and reset times, and the lanes that are alive and hold a task. It keeps the
   last 3 hours. A rate is the rise in percentage points over a span, divided by the span
   in hours and by the mean busy lanes, from the oldest sample in the window that shares
   the newest sample's reset time. No rate when the mean is below 0.5 lanes. The five-hour
   rate uses a 60-minute window and needs a 30-minute span; the weekly rate a 3-hour window
   and a 60-minute span, because whole-point readings need that many points before one
   point of rounding stops swinging the rate.

3. **Pace targets**, in fractional lanes: the lanes that spend each window up to
   `-StopAtUsage` (85%) or `-StopAtWeeklyUsage` (97%) just as it resets. A zero rate or a
   reset already due makes a target unbounded. The pace target is the five-hour target;
   with `-WeeklyPace` it is the lower of the two. By default the weekly window only stops
   claims at 97%: pacing it finishes no more work than burning to 97% and waiting, and
   loses whatever an idle factory leaves at the reset. A shift that finds the week used up
   waits for its reset with a notice, not the alarm.

4. **Ceilings.** `ceiling = min(capacity, machineCap, MaxLanes)`. `capacity` is
   `task-board.ps1 capacity`: the tasks in `Doing` plus the ready tasks that could start
   beside them without overlapping `touches`, read at `origin/<branch>` in a detached
   worktree (`<repo>.lanes\auto-board`) so the coordinator's checkout - whose script the
   lanes run - is pulled only at shift end.

5. **One step.** `desired = min(pace target, ceiling)`. Up by one lane when
   `desired >= current + 1`, so the meter samples each count. Down when
   `desired < current - 0.25`, straight to `max(1, floor(desired + 0.25))`: at once for a
   ceiling, and for a pace target only when the previous step was low too (the first low
   step holds and traces `low once`). It holds when no five-hour rate is known yet, when
   `Get-UsageStop` already says stop, and when the shift's time is up.

6. **Scaling mechanics.** Adding starts the lowest free lane number through
   `Start-Detached` (a herdr tab in Surl's own workspace, or a console window). Retiring
   writes `lane-<n>.retire` for the highest-numbered active lanes, as many as the step
   drops; a lane sees it at the top of its loop, before its next claim, and stops with
   `retired`. A lane never retires mid-task, mid-integration or while resuming a held task.

7. **Machine-cap probe** (`-ProbeMachine`). After one untimed warm-up build it runs
   `k = 1, 2, ...` concurrent `dotnet build --no-incremental` of the checkout, each with its
   own `--artifacts-path` under `<repo>.lanes\probe`, recording wall time and the lowest free
   physical memory. A step passes when every build succeeds and at least 20% of memory
   stays free; wall time and slowdown are recorded for reading only, because a slow build
   costs minutes while running out of memory stalls every lane. `machineCap` is the largest
   passing `k`, at least 1, stopping at the first failure, 16 or `-ProbeMaxLanes` (then
   marked incomplete). It writes `<repo>.lanes\machine-lanes.json` (`schema`, `probedAt`,
   `logicalProcessors`, `memoryGB`, `complete`, `cap`, `rule`, `steps[]`). An Auto shift
   probes before any lane starts when that file is missing, incomplete, from other
   hardware, or recorded under another `rule`. The finding lives on the machine, not in the
   repository, because it describes one PC.

8. **Cold start.** `<repo>.lanes\auto-lanes.json` (`schema`, `lanes`, `savedAt`,
   `fiveHourRatePerLane`, `weeklyRatePerLane`) is written at every lane change and at shift
   end. An Auto shift starts at the saved `lanes` raised to `-MinStartLanes` (default 16),
   capped by the ceilings, and meters from the saved rates until it has its own - so by
   default it starts at its ceiling and dials down. `-MinStartLanes` is a start, not a
   floor. Adopted lanes numbered above the start count raise it, as with a fixed count.
   `-Continuous` hands on `-Lanes Auto`, `-MaxLanes` and `-MinStartLanes`.

9. **The log line.** Every step traces `lanes <old> -> <new> (<binding limit>)` or
   `lanes <n> held (<binding limit>)`, the limit being one of `5-hour pace allows 4.7`,
   `weekly pace allows 5.2`, `6 ready tasks can run at once`, `machine sustains 7` or
   `lane maximum 6`, with one decimal in the invariant culture.

10. **`status.json` gains a top-level `autoLanes` object**, additive to ADR-0029's
    schema 1: `{ "lanes", "target", "binding", "reason", "changedAt" }`, `null` on a
    fixed-lane shift. The schema stays 1 because the page ignores fields it does not know;
    the page shows it above the lane cards.

## Consequences

- The factory spends what the current plan sustains with no per-plan setting; a plan
  change shows up as a different measured rate within one or two steps.
- `-TestAutoLanes` and `-TestMachineProbe` prove the pure logic on recorded readings, and
  `-AutoLanesReport` prints one step's reading without starting a lane.
- Scaling up is slow on purpose (one lane per 15 minutes); scaling down is immediate once
  confirmed, so a shift that starts too wide narrows before the five-hour stop.
- The first Auto shift on a machine, and any after a hardware change, spends a few minutes
  on the probe before a lane starts. Run `-ProbeMachine` only when no shift is building.

## Alternatives considered

- **A per-plan lane table.** It needs retuning whenever the plan changes.
- **Several lanes up per step.** It overshoots on a noisy whole-point meter.
- **Killing a surplus lane.** It loses work mid-task; retiring before the next claim loses none.
- **Guessing the machine cap from the processor count.** Builds are memory-bound too; only
  a measurement is trustworthy.

## Amendment 1 (2026-11-05): reading a window that is not reported

Decided by Claude under Stewart's delegation, after Stewart asked for the usage reading to be fixed.

Since an October 2026 Claude Code update, a `rate_limit_event` no longer lists every window
under `unifiedWindows`. It lists only the window it is about, named by `rateLimitType`, with
`utilization` and `resetsAt` beside it, and leaves a window out until it nears its limit.
`Get-UsageReading` demanded a `five_hour` figure, found none after 2026-09-30, and returned no
reading at all. Every Auto step then logged "lanes N held (no burn rate yet)", so the shift of
2026-11-04 held 5 lanes for seven hours while the board's capacity rose to 33.

1. `ConvertFrom-RateLimitEvent` reads each window from either shape. A window under
   `unifiedWindows` wins over the top-level fields.
2. `Get-UsageReading` takes each window from the newest event that reports it, across the
   newest 40 runs. It returns a reading once any `rate_limit_event` exists. A window no event
   reports reads 0 with no reset, and `FiveHourReported` and `WeekReported` say which were
   seen. An unreported window is not near its limit, so it sets no pace. The stops at
   `-StopAtUsage` and `-StopAtWeeklyUsage` still apply as soon as Claude Code reports the
   window.
3. `-AutoLanesReport` prints "not reported (not near its limit)" for such a window rather
   than a 0% that was never measured.
4. `-TestAutoLanes` proves both event shapes, a non-event line, and that a shift with no
   5-hour report grows toward its ceiling.

## Amendment 2 (2026-11-05): the end-of-shift merge comes from a frozen branch

Decided by Stewart, who chose this option and gave the standing permission it needs.

The end-of-shift pull request used to come from the shift branch itself. Under `-Continuous`
the next shift's lanes push to that branch within minutes, and each push moved the pull
request's head and cancelled its CI. A merge that waited 30 minutes also found CI queued
behind the account's other runners, macOS above all. No shift merged from 2026-11-04 to
2026-11-05.

1. At shift end `Open-MergeSnapshot` pushes the branch's head to `factory/merge-<stamp>`
   and opens the pull request from that frozen branch. Nothing pushes to it, so its CI
   always runs to the end.
2. Opening one closes every older open `factory/merge-*` pull request, deleting its branch:
   the newer head holds everything the older did. It also closes a pull request opened
   from the shift branch itself under the old design, and keeps that branch.
3. `Update-PendingMerges` runs at the start of every coordinator loop and every 5 minutes.
   It merges a frozen pull request with `--match-head-commit` once CI passed on its frozen
   commit, and deletes the branch. It reruns a cancelled CI once, and reports a failure
   once. A failed pull request stays open until a newer shift supersedes it.
4. A `-Continuous` shift leaves the merge to the next shift's coordinator and starts at
   once. Without `-Continuous`, the ending shift waits up to 30 minutes for it.
5. `-MergeToMaster` does the end-of-shift step by hand. `-TestMergeSnapshot` proves the
   naming, the supersede rule and the step each CI state leads to.

Stewart's standing permission (CLAUDE.md, "Git and GitHub") covers creating, merging and
deleting `factory/merge-*` branches, and closing a superseded frozen pull request. It covers
no other branch and no force push.

## Amendment 3 (2026-11-06): a task that times out is split, not blocked

Decided by Claude under Stewart's delegation, after Stewart asked for the factory to split
timed-out tasks itself.

A run cut off at `-TaskMinutes` used to send its task to Blocked as a question for Stewart.
Seven tasks timed out between 2026-11-05 and 2026-11-06 (BL-395, BL-653, BL-595, BL-597,
BL-545, BL-816 and BL-834). Each needed only a split, never his judgement, and every split
was done by hand.

1. When a lane's run times out with its task still in Doing, the lane stashes the run's
   work as `darkfactory <id> <stamp>`, as before. It then starts a split run of at most
   45 minutes with `$SplitPrompt`. That run follows the task-planner agent's sizing limits
   and files smaller tasks with `task-board.ps1 new`. Each new task names the stash files
   it reuses, taken with `git diff ... | git apply` or `git show "stash@{N}^3:<path>"`,
   because lanes may not run `git checkout` or `git stash`. The run moves the original to
   Deferred, repoints its dependents and commits only `Tasks/`.
2. The run's deny list (`$SplitForbidden`) forbids pushing, fetching, rebasing,
   checking out, every stash command that changes the stash list, and `dotnet`.
3. A task that ends in Deferred integrates like any other finished task and counts as
   `split`, not as a failing run.
4. If the split does not end with the task in Deferred, the lane resets to the commit
   before the split. The work is safe in the stash. The lane then blocks the task as
   before, with the reason "timed out ... and could not be split".
5. The task-planner agent and the task-board skill now size tasks for about 60 minutes
   (two projects, one protocol, about 300 lines of new production code, one kind of
   work, six acceptance criteria), so fewer tasks time out in the first place.
