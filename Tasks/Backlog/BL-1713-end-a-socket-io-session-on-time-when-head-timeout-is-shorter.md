---
id: BL-1713
title: End a Socket.IO session on time when --head-timeout is shorter than the wait its clock is already in
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1713 — End a Socket.IO session on time when --head-timeout is shorter than the wait its clock is already in

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

- [ ] A test with a 5-second head timeout, using `ManualTimeProvider`, leaves the last namespace after the first
      pong and sees WebSocket Close `1000` 5 seconds later.
- [ ] The test waits on an observable event before each clock advance, with no real-time delay added.
- [ ] `dotnet test Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests` passes.

## Notes

## Log

- 2026-11-25: Created.
