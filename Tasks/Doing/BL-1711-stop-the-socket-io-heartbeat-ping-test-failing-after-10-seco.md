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
completed:
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

- [ ] The task's Notes state the cause of the 10-second failure, naming the code that orders the clock advance
      against the session.
- [ ] `RunAsync_Heartbeat_PingsEveryPingIntervalAfterThePong` waits on an observable event before each clock
      advance that depends on it, with no real-time delay added.
- [ ] `dotnet test Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests` passes 20 runs in a row.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
