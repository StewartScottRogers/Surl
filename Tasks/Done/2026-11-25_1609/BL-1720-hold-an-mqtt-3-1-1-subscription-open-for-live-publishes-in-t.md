---
id: BL-1720
title: Hold an MQTT 3.1.1 subscription open for live publishes in the MQTT server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Documentation/Planning/Decisions/ADR-0014-how-the-mqtt-server-answers.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
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

- [x] Without a hold count every existing `Surl.Protocol.Mqtt.UnitTests` test passes unchanged.
- [x] Tests show: the subscription is on the bus before `SUBACK` is written, a live publish is sent once with fixed header `0x30` under the scheme's name, and `DISCONNECT` follows the count-th `PUBLISH`, retained ones counted; count 0 never sends it.
- [x] Tests show: while held, `PINGREQ`, `UNSUBSCRIBE`, a second `SUBSCRIBE` and a client `DISCONNECT` are answered as the amendment says, and no Keep Alive timeout closes the connection.
- [x] Tests show: a first `SUBSCRIBE` with every filter refused is ended with `DISCONNECT`, and a backlog past 1000 messages or 16 MiB closes with no bytes and a log note.
- [x] `dotnet build` is clean and the library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

- Design: ADR-0014 Amendment 1 already held every decision, so no new ADR. Built `MqttHeldSubscription` (bus
  subscriptions, bounded backlog, count), `MqttPacketResponder.HeldSubscription.cs` (the send-while-reading loop),
  `MqttPendingPacketRead` and `MqttLivePublish`. The constructor value is `int? holdSubscriptionCount = null`.
- One loop does every write and one read runs beside it, so no write gate is needed. A backlog overflow cancels a
  token the held connection's reads and writes use, so a client that stops reading is closed too.
- Choices where the amendment is silent: "the same filter again" is matched on the filter as sent and keeps the bus
  subscription (nothing is missed), reading its retained messages again; a message that expires while it waits is
  neither sent nor counted; a filter past the bus's 1000 subscriptions a connection is `SUBACK` `0x80` with its own
  log note; a command-stage delay rule's hold finishes before an overflow closes the connection.
- At the count-th `PUBLISH` queuing stops before it is written, so a backlog cannot cancel the `DISCONNECT` (found in
  review; reproduced by a failing test first).
- `touches` widened to ADR-0014: its "Not yet built: BL-1720" became false. No task in Doing names that file.
- Follow-up filed: BL-1732 (a retained publish landing between two filters of one `SUBSCRIBE` is sent twice).
- Verified: build 0 warnings, format clean, fast tests 29171 passed, 0 failed, 3 skipped; library 100% line and
  branch, 0 failing members; the Conformance project's 47 MQTT `Integration` tests pass. Pinned-curl proof of the
  held mode is BL-1722.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. MqttProtocolServer given a hold count keeps a SUBSCRIBE open, sends later publishes and ends with DISCONNECT at the count
