---
id: BL-967
title: Save the message bus after each AMQP 0-9-1 change to a durable queue
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-732, BL-1035]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-967 — Save the message bus after each AMQP 0-9-1 change to a durable queue

## Goal

A durable queue declared, published to, consumed from or bound on `amq.topic` over AMQP 0-9-1 is written to the
bus's files (`.surl/bus`) after the frame that changed it, so it survives a restart under `--directory`.

## Context

- ADR-0112 decision 6: "Storage across restarts is the bus's" (ADR-0100 decision 3.7). `MessageBus.SaveChangesAsync`
  writes every change since the last save and returns `BusSaveResult`; a server calls it after each change it makes
  (see `MqttPacketResponder`, `Mqtt5BrokerConnection`, `Amqp1ContainerConnection.Transfers`), noting
  `MessageBusOutcome.StorageFailed` with `IExchangeLog.Note` and never ending.
- `Amqp0BrokerConnection` never calls it (found by BL-732). The natural place is beside
  `SaveExchangesAsync` in `AnswerChannelFrameAsync` (`Amqp0BrokerConnection.Channels.cs`), and after
  acknowledgements settle deliveries.

## Acceptance criteria

- [ ] A named `Amqp0BrokerConnectionTests` test: with a bus loaded from `MessageBusFiles` on an
      `InMemoryContentFileSystem`, a durable queue declared and published to over AMQP 0-9-1 holds its message after
      a second bus loads the same files.
- [ ] A named test: a failed bus write is noted in the exchange log and the connection goes on.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-11 (lane 6): Code and tests written but not built: `dotnet` refuses to run because `global.json` pins SDK
  10.0.401, which this machine does not have (installed: 10.0.100-rc.1, 11.0.100-rc.2, 11.0.100). Returned to Backlog
  behind BL-1035, as BL-941 was. The uncommitted work, stashed by the shift: `SaveBusAsync` in
  `Amqp0BrokerConnection.Exchanges.cs`, called after `SaveExchangesAsync` in `AnswerChannelFrameAsync`, noting
  `StorageFailed` as "The message bus could not save its files (<reason>); the change is kept in memory."; a bullet in
  the library's `CLAUDE.md`; and `Amqp0BrokerConnectionTests.BusFiles.cs` with the two named tests. Choice: a no-ack
  consumer delivery (made on the bus's thread) is written by the next channel frame's save, not from `Deliver`,
  which must never block.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds or tests
- 2026-11-12: Backlog -> Doing.
