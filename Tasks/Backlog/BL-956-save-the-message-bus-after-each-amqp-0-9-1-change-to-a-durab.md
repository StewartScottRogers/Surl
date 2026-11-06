---
id: BL-956
title: Save the message bus after each AMQP 0-9-1 change to a durable queue
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-732]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-956 — Save the message bus after each AMQP 0-9-1 change to a durable queue

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

## Log

- 2026-11-06: Created.
