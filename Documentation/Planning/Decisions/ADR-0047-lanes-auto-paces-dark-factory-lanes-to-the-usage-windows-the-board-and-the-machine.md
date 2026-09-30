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
