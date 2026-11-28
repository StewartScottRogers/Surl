---
id: BL-2132
title: Make a one-shot MQTT read wait for unacknowledged publishes from NATS, STOMP, AMQP and Redis connections
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests, Protocols/Messaging/Surl.MessageBus.UnitLibrary, Documentation/Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2132 — Make a one-shot MQTT read wait for unacknowledged publishes from NATS, STOMP, AMQP and Redis connections

## Goal

A message published without an acknowledgement over NATS, STOMP, AMQP or Redis is on the bus before a
later connection's one-shot MQTT `SUBSCRIBE` is answered, or ADR-0100 states why a server need not join.

## Context

BL-2117 (ADR-0100 decision 3.11): a one-shot MQTT `SUBSCRIBE` waits for earlier connections that joined
`MessageBus.PublishingConnections`. Only `mqtt`, `mqtts` and the MQTT 5 broker join. A stand-in test that
publishes over another protocol without waiting for an answer (NATS `PUB` without verbose, STOMP `SEND`
without `receipt`, AMQP 0-9-1 `basic.publish` without confirms, AMQP 1.0 pre-settled transfers) and at
once fetches with `curl mqtt://` has no ordering between the two. See `MqttPacketReader` for how a
server joins. Too large for one run if all five need work: split by server.

## Acceptance criteria

- [ ] For each server above, either a unit test shows its connection joins and counts as caught up only
      while it waits, or ADR-0100 decision 3.11 states why its publishes need no wait.
- [ ] For each server that joins, a unit test publishes without acknowledgement, then reads one-shot with
      no clock advance and receives the message.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
