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
completed:
---
# BL-1885 — Fix the intermittent command-stage delay idle-timeout test in Surl.Core.UnitTests

## Goal

`ServeAsync_CommandStageDelayLongerThanTheIdleTimeout_IdleTimeoutDoesNotFireDuringIt` passes on every run, whatever order the engine creates its timers in.

## Context

- Seen 2026-11-27 during BL-1858 on dark factory lane 3: it failed after 10 s in a full fast-test run on a loaded machine, and all 346 tests of `Surl.Core.UnitTests` passed on the rerun. BL-1858 changed nothing in Core.
- File: `Foundation/Surl.Core.UnitTests/ServingEngineTests.Faults.cs`.
- Unverified reading: the test advances `ManualTimeProvider` after `TimersCreated(2)`; if those two timers can exist before the delay timer does, the advance comes too early and the delay never ends.

## Acceptance criteria

- [ ] The test waits for the delay timer itself before advancing the clock, or the Notes state the real cause and the change that removes it.
- [ ] The named test passes 50 times in a row.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
