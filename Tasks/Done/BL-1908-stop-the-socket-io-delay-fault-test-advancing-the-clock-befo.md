---
id: BL-1908
title: Stop the Socket.IO delay fault test advancing the clock before the hold starts
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1908 — Stop the Socket.IO delay fault test advancing the clock before the hold starts

## Goal

`SocketIoFaultTests.Connect_UnderADelayRule_IsServedAfterTheHold` advances the manual clock only after the delay's
timer is counting on it, so it cannot time out under load.

## Context

- The test failed once (10 s timeout) in a full fast-test run on Windows on 2026-11-27, during BL-1899, and passed
  when its project was run alone.
- Likely cause, read from the source and not reproduced: the test waits for the `FaultAppliedEvent`, but
  `CommandStageFaults.HoldAsync` publishes it before it calls `Task.Delay`. The clock can then advance before the
  timer exists, and the hold never ends.
- `HoldAsync` starts the wait before it pauses the idle clock, so waiting for that pause, or for the timer to be
  registered on `ManualTimeProvider`, is a safe signal.

## Acceptance criteria

- [x] The test waits for a signal that is raised only after the delay's timer exists before it advances the clock.
- [x] `dotnet test Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests` passes.

## Notes

- The test now calls `ManualTimeProvider.AdvanceWhenWaitingAsync(1 ms)`, which waits until a timer due at that instant
  is registered on the clock. Chosen over waiting for the idle-clock pause: the project already has this helper, and it
  observes the timer itself.
- No separate failing test first: the race depends on scheduling and cannot be forced without a production hook.
- Verified 2026-11-27 on Windows: project tests 291 of 291; build 0 warnings; format check clean. The first full fast
  run had one unrelated failure in `Surl.Protocol.Ws.UnitTests` (filed as BL-1909); the second passed (30275 passed,
  0 failed, 3 skipped).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The Socket.IO delay fault test advances the clock only once the delay's timer exists
