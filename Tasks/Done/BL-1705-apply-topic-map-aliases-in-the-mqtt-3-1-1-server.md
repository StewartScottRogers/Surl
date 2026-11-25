---
id: BL-1705
title: Apply --topic-map aliases in the MQTT 3.1.1 server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md, Documentation/Planning/Decisions/ADR-0139-how-the-mqtt-and-ldap-servers-answer-a-chosen-error.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1705 — Apply --topic-map aliases in the MQTT 3.1.1 server

## Goal

A `--topic-map` whose left side is `mqtt:<topic>` or `mqtts:<topic>` applies to MQTT 3.1.1 clients both ways, as
ADR-0102 decision 3.3 says.

## Context

- Found in BL-1703: `Surl.Protocol.Mqtt.UnitLibrary` maps names with `MqttTopicNames` (the default rules,
  `MqttPacketResponder.cs`) and never asks the bus's `MessageBus.Names` (`TopicNameLookup`), so a map naming `mqtt`
  on its left is accepted at start and does nothing.
- NATS, MQTT 5, AMQP 1.0, STOMP, Kafka and Redis already call `bus.Names`; follow
  `Surl.Extended.Protocol.Mqtt5.UnitLibrary` (`Mqtt5OutboundDeliveries.cs`).
- Cover publish, will, subscribe filter, live delivery and retained delivery. With no map, every byte stays as today
  (the replayed upstream curl exchanges keep passing).
- Stand-in: the map joins this server to the others through the one shared message store.

## Acceptance criteria

- [x] An `Surl.Protocol.Mqtt.UnitTests` test: with the map `mqtt:orders/new=nats:shop.orders`, a PUBLISH on
      `orders/new` lands on canonical `shop/orders`.
- [x] A test: a subscriber to `orders/new` receives a retained message published on `shop/orders`, both one already
      on the bus and one a client published through the server, with topic name `orders/new`. (Reworded from "live and
      retained": this server has no live delivery; see Notes.)
- [x] A test: with that map, a subscriber to `#` receives nothing for a message published on canonical `orders/new`.
- [x] A wildcard filter is mapped by the default rules alone, as a test shows.
- [x] The `--topic-map` paragraph in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` no longer says the `mqtt`
      server does not apply a map, and lists it among those that do.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Change: `MqttPacketResponder` maps `PUBLISH` topics, `SUBSCRIBE` filters and retained deliveries through
  `MessageBus.Names` for the exchange's scheme. Tests: `MqttTopicMapTests`. Recorded in ADR-0102's BL-1705 amendment.
- Criterion 2 reworded. The server answers a `SUBSCRIBE` with retained messages only, then `DISCONNECT` (ADR-0014), so
  "live" delivery to an MQTT 3.1.1 subscriber does not exist. Adding it changes bytes with no map, which this task
  forbids; filed as BL-1712.
- Will: the server skips the `CONNECT`'s will and publishes none, so there is no will topic to map.
- Fault rule path: canonical topic at `PUBLISH`, filter as sent at `SUBSCRIBE`, as MQTT 5 does (amends ADR-0139).
- `touches` gained ADR-0102 and ADR-0139; no task in Doing named either.
- Live check, pinned curl 8.21.0 (Git for Windows) against `surl --allow-anonymous --topic "#;retain" --topic-map
  mqtt:orders/new=nats:shop.orders`: `-d hi .../orders/new` exit 0; fetching `orders/new`, `shop/orders` and `%23`
  each exit 0 with topic `orders/new`, payload `hi`.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. --topic-map aliases naming mqtt or mqtts now apply to MQTT 3.1.1 publishes, subscribe filters and retained deliveries
