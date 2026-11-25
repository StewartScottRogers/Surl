---
id: BL-1720
title: Hold an MQTT 3.1.1 subscription open for live publishes in the MQTT server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1720 — Hold an MQTT 3.1.1 subscription open for live publishes in the MQTT server

## Goal

`MqttProtocolServer`, given a hold count, keeps a `SUBSCRIBE` open, sends later publishes to the subscriber and ends it with `DISCONNECT` after that many messages; without one it answers as today.

## Context

- Follow ADR-0014 Amendment 1 exactly (BL-1715); it holds the decisions and the pinned curl 8.21.0 measurements.
- Start at `MqttPacketResponder.cs` (`DeliverRetainedAsync`) and `MqttBusRetainedMessages.cs`; the count is an
  optional constructor value of `MqttProtocolServer`, absent by default.
- The server must now read client packets while it writes live publishes; `BusConnection.AfterMessageDelivered` gives
  one call per message and connection, whatever filters overlap.
- Update the `MqttProtocolServer` remarks and the library's `CLAUDE.md`.

## Acceptance criteria

- [ ] Without a hold count every existing `Surl.Protocol.Mqtt.UnitTests` test passes unchanged.
- [ ] Tests show: the subscription is on the bus before `SUBACK` is written, a live publish is sent once with fixed header `0x30` under the scheme's name, and `DISCONNECT` follows the count-th `PUBLISH`, retained ones counted; count 0 never sends it.
- [ ] Tests show: while held, `PINGREQ`, `UNSUBSCRIBE`, a second `SUBSCRIBE` and a client `DISCONNECT` are answered as the amendment says, and no Keep Alive timeout closes the connection.
- [ ] Tests show: a first `SUBSCRIBE` with every filter refused is ended with `DISCONNECT`, and a backlog past 1000 messages or 16 MiB closes with no bytes and a log note.
- [ ] `dotnet build` is clean and the library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
