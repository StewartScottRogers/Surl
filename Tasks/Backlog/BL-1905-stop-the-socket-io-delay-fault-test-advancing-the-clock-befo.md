---
id: BL-1905
title: Stop the Socket.IO delay fault test advancing the clock before the hold starts
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1905 — Stop the Socket.IO delay fault test advancing the clock before the hold starts

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

- [ ] The test waits for a signal that is raised only after the delay's timer exists before it advances the clock.
- [ ] `dotnet test Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests` passes.

## Notes

## Log

- 2026-11-27: Created.
