---
id: BL-1786
title: Make the refusal settle delay test in Surl.Core wait for the timer instead of 50 ms
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1786 — Make the refusal settle delay test in Surl.Core wait for the timer instead of 50 ms

## Goal

`ServeAsync_RefusalSettleDelay_HoldsTheRefusalUntilTheDelayHasPassed` passes however slowly the machine runs, because
it waits for the engine to reach its settle delay and no longer sleeps for 50 ms of real time.

## Context

- Seen during BL-1772 on 2026-11-26: it failed after 10 s in a `Measure-CodeQuality.ps1` coverage run of the whole
  solution, and passed in the fast run before it and in a run of `Surl.Core.UnitTests` alone after it. A failing test
  stops the quality measurement.
- `Foundation/Surl.Core.UnitTests/ServingEngineTests.Limits.cs`: the test advances its fake clock after
  `Task.Delay(50)`. If the timer is not created by then, the likely cause, the advance does not fire it.

## Acceptance criteria

- [x] The test holds no `Task.Delay` and advances the clock only after a signal that the settle delay's timer exists.
- [x] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

- The signal is `ManualTimeProvider.TimersCreated(count + 1)`, counted from before the second connection. With
  `OneConnection` the first exchange creates no timer (both exchange timeouts are zero), so the next timer is the
  settle delay's.
- The second 50 ms sleep, after the first advance, gave a wrongly early refusal time to appear. It is replaced by the
  new `ManualTimeProvider.ArmedTimerCount`: one armed timer after `delay - 1 tick` proves, with no waiting, that the
  settle delay's timer has not fired, so a delay shorter than the default fails the test every time.
- 2026-11-26: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests
  30023 passed, 3 skipped, 0 failed.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The refusal settle delay test waits for the delay's timer instead of sleeping 50 ms, so a slow machine no longer fails it
