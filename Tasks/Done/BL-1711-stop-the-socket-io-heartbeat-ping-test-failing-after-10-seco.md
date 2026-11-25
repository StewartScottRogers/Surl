---
id: BL-1711
title: Stop the Socket.IO heartbeat ping test failing after 10 seconds on a loaded machine
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1711 — Stop the Socket.IO heartbeat ping test failing after 10 seconds on a loaded machine

## Goal

`SocketIoSessionTests.RunAsync_Heartbeat_PingsEveryPingIntervalAfterThePong` passes however the machine's load
orders the test against the server's session.

## Context

- Seen once on 2026-11-25 (lane 4, BL-1699, nothing in Socket.IO changed): the full fast-test run reported
  `Failed RunAsync_Heartbeat_PingsEveryPingIntervalAfterThePong [10 s]`, 289 of 290 passed in
  `Surl.Extended.Protocol.SocketIo.UnitTests`; the project alone and two later full runs passed. The failure's
  message was not captured.
- Unconfirmed lead: `Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests/SocketIoSessionTests.cs` advances
  the fake clock with `AdvanceWhenWaitingAsync` after sending client messages the session may not have read yet,
  so an advance may land on a timer the session is about to replace.

## Acceptance criteria

- [x] The task's Notes state the cause of the 10-second failure, naming the code that orders the clock advance
      against the session.
- [x] `RunAsync_Heartbeat_PingsEveryPingIntervalAfterThePong` waits on an observable event before each clock
      advance that depends on it, with no real-time delay added.
- [x] `dotnet test Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests` passes 20 runs in a row.

## Notes

- Cause: `SocketIoSession.SendPing` sets `pingSentAt` and queues the ping before `RunClockAsync` calls
  `Deadlines()` and arms its next `Task.Delay`. A pong the read loop hands to `AcceptPong` inside that window
  clears `pingSentAt`, so the clock arms the next ping's timer (12:00:50) instead of the pong deadline
  (12:00:45). `ManualTimeProvider.AdvanceWhenWaitingAsync(20 s)` then waits for a 12:00:45 timer that never
  exists and throws `TimeoutException` after 10 real seconds. The server behaves correctly in that order; the
  test assumed the other one.
- Two pongs could fall in the window: the one sent after the first ping, and the early one the test sent before
  any ping without waiting for the session to read it.
- Reproduced with temporary lines in `SocketIoSession.cs` (removed, not committed): `Thread.Sleep(200)` after
  the ping failed the heartbeat test with `No timer became due at 2026-11-05T12:00:45`; `await Task.Yield()`
  plus the sleep failed it and `Disconnect_LastNamespace_StartsTheConnectTimeout` the same way. The fixed tests
  passed 290 of 290 under both.
- Fix, test only: the early pong is followed by an event whose echo is awaited before the first advance, and each
  pong that answers a ping is sent only after `WaitForPongDeadlineTimerAsync` sees the 20-second timer.
- `Disconnect_LastNamespace_StartsTheConnectTimeout` had the same race and got the same wait: same file, same
  cause, and it would have failed the 20-run criterion's project the same way.
- Verified 2026-11-25: 20 of 20 project runs passed (290 tests each); build 0 warnings; format check clean; fast
  tests 29022 passed, 0 failed, 3 skipped.
- Filed BL-1716 for a separate library lateness found while reading the clock.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The Socket.IO heartbeat and disconnect tests wait for the pong-deadline timer before sending a pong, so thread order no longer fails them
