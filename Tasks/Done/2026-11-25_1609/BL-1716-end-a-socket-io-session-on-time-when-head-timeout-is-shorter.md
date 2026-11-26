---
id: BL-1716
title: End a Socket.IO session on time when --head-timeout is shorter than the wait its clock is already in
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1716 — End a Socket.IO session on time when --head-timeout is shorter than the wait its clock is already in

## Goal

A Socket.IO session whose last namespace is left ends `--head-timeout` after the leave, also when that is
sooner than the next ping or pong deadline.

## Context

- Found by reading the code during BL-1711 on 2026-11-25; not yet reproduced by a test.
- `SocketIoSession.RunClockAsync` (`Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoSession.cs`)
  sleeps until the earliest of the deadlines it read. `Leave` sets `noNamespaceSince` without waking it, so with
  `--head-timeout` 5 s the clock can sleep up to the 25-second ping deadline before it sees the new connect
  deadline.
- ADR-0120, decisions 2 to 7, gives the heartbeat and the connect timeout.

## Acceptance criteria

- [x] A test with a 5-second head timeout, using `ManualTimeProvider`, leaves the last namespace after the first
      pong and sees WebSocket Close `1000` 5 seconds later.
- [x] The test waits on an observable event before each clock advance, with no real-time delay added.
- [x] `dotnet test Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests` passes.

## Notes

- Reproduced before the fix: the new test
  `Disconnect_LastNamespaceWithAHeadTimeoutEndingBeforeThePongDeadline_ClosesWhenItEnds` failed with no timer due
  5 seconds after the leave (the clock was asleep until the pong deadline).
- Fix: `Leave` completes a wake task the clock's sleep waits on (`Task.WaitAsync` with the injected
  `TimeProvider`, so the unused timer is disposed). Only a last leave can move a deadline sooner (a pong and a
  connect move them later), so it is the only caller that wakes the clock. This implements ADR-0120 as written;
  no new decision, so no ADR.
- `Disconnect_LastNamespace_StartsTheConnectTimeout` pinned the old timer sequence (sleeping on to the pong
  deadline already answered); the woken clock now sleeps straight to the next ping, 25 seconds on.
- Verified 2026-11-25: build 0 warnings, format clean, fast tests all pass; SocketIo 291/291 in 6 runs.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. A Socket.IO session whose last namespace is left now ends --head-timeout after the leave, also when that is before the next ping or pong deadline.
