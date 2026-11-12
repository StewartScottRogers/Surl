---
id: BL-919
title: Make RunAsync_OutputFailsForARunningCall_ThrowsTheFailureOnceTheSessionEnds pass every run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1035]
touches: [Protocols/Http/Surl.McpServing.UnitTests, Protocols/Http/Surl.McpServing.UnitLibrary]
requirement: none
created: 2026-11-06
completed:
---
# BL-919 — Make RunAsync_OutputFailsForARunningCall_ThrowsTheFailureOnceTheSessionEnds pass every run

## Goal

`Surl.McpServing.UnitTests`' `RunAsync_OutputFailsForARunningCall_ThrowsTheFailureOnceTheSessionEnds` passes on every run, not most.

## Context

- Seen by BL-548's lane on 2026-11-06: one fast-test run of the whole solution failed it
  (`Failed RunAsync_OutputFailsForARunningCall_ThrowsTheFailureOnceTheSessionEnds [58 ms]`), and the next
  three runs of the project alone passed it. BL-548 touched nothing in `Surl.McpServing`, so it is a race in the
  test or in `McpStreamSession`'s handling of an output failure during a running call.
- A flaky fast test blocks the dark factory's merge to `master` when it fails on CI.

## Acceptance criteria

- [ ] The race is named under Notes (what ordering makes the test fail) and fixed in the test or the library.
- [ ] `dotnet test Protocols/Http/Surl.McpServing.UnitTests --filter "Name~RunAsync_OutputFailsForARunningCall"` passes 50 runs in a row.
- [ ] `Surl.McpServing.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 6): Returned to Backlog behind BL-1035. `dotnet build` fails here because the
  pinned SDK 10.0.401 is not installed, so no fix could be built or tested.
- The race, found by reading the code and not yet confirmed by a run: in `McpStreamSession`,
  `FinishRunningAsync` awaits `FinishAsync`, and `FinishAsync`'s `finally` calls
  `_requestSlots.Release()` *before* the `IOException` reaches `FinishRunningAsync`'s `catch`,
  which records `_runningAnswerFailure`. Once the test calls `input.Complete()`,
  `EndRunningAnswersAsync` takes back all `MaximumConcurrentRequests` slots. If it takes the
  released slot between that `Release()` and the `Interlocked.CompareExchange`, it reads
  `_runningAnswerFailure` as null and the session ends without throwing. The test then fails:
  "no IOException thrown". The bug is in the library, not the test.
- Planned fix: record the failure before releasing the slot. For example, catch the write
  failure inside `FinishAsync`, ahead of its `finally`, or have `FinishRunningAsync` release the
  slot itself after the `catch`. Then a test that holds the session between the release and the
  record proves the fix (TDD rule), then 50 runs.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build fails; race named under Notes
- 2026-11-12: Backlog -> Doing.
