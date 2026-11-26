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
completed:
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

- [ ] The test holds no `Task.Delay` and advances the clock only after a signal that the settle delay's timer exists.
- [ ] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-26: Created.
