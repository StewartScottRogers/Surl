---
id: BL-1885
title: Fix the intermittent command-stage delay idle-timeout test in Surl.Core.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1885 — Fix the intermittent command-stage delay idle-timeout test in Surl.Core.UnitTests

## Goal

`ServeAsync_CommandStageDelayLongerThanTheIdleTimeout_IdleTimeoutDoesNotFireDuringIt` passes on every run, whatever order the engine creates its timers in.

## Context

- Seen 2026-11-27 during BL-1858 on dark factory lane 3: it failed after 10 s in a full fast-test run on a loaded machine, and all 346 tests of `Surl.Core.UnitTests` passed on the rerun. BL-1858 changed nothing in Core.
- File: `Foundation/Surl.Core.UnitTests/ServingEngineTests.Faults.cs`.
- Unverified reading: the test advances `ManualTimeProvider` after `TimersCreated(2)`; if those two timers can exist before the delay timer does, the advance comes too early and the delay never ends.

## Acceptance criteria

- [x] The test waits for the delay timer itself before advancing the clock, or the Notes state the real cause and the change that removes it.
- [x] The named test passes 50 times in a row.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Real cause (the Context's reading was wrong): the exchange creates only two timers, the idle clock's and then the
  delay's. `CommandStageFaults.HoldAsync` creates the delay timer and only afterwards calls `IdleClock.Pause()`.
  `TimersCreated(2)` completes inside the delay timer's creation, so on a loaded machine the test could
  `Advance` 5 s before the pause. The idle timeout then fired, cancelled the hold, `held` was never set, and
  the test failed after the 10 s `Patience.Timeout`.
- Change: the test's server wraps the engine's idle clock in `PauseAnnouncingIdleClock` (new, in
  `Surl.Core.UnitTests`), which forwards to it and completes `Paused`. The test advances only after that.
  `RecordingIdleClock` was not used because it replaces the engine's clock, which this test is about.
- Production code is unchanged: its order (delay timer, then pause) is what makes waiting for the pause enough.
- Verified 2026-11-27: with a temporary 500 ms sleep before `Pause()`, the old test failed after 10 s and the new
  one passed; the sleep was removed. Then 50 of 50 runs passed, `dotnet build -warnaserror` had 0 warnings, and
  30229 fast tests passed.
- The neighbouring Accept-stage test (`ServeAsync_DelayLongerThanTheIdleTimeout_DoesNotIdleTheExchangeOut`) has no
  such gap: the engine pauses the idle clock before it creates the delay timer.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The command-stage delay idle-timeout test waits for the idle clock's pause before advancing, so it no longer fails intermittently.
