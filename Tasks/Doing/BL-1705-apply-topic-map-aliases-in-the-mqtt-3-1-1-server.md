---
id: BL-1705
title: Apply --topic-map aliases in the MQTT 3.1.1 server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed:
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

- [ ] An `Surl.Protocol.Mqtt.UnitTests` test: with the map `mqtt:orders/new=nats:shop.orders`, a PUBLISH on
      `orders/new` lands on canonical `shop/orders`.
- [ ] A test: a subscriber to `orders/new` receives a message published on `shop/orders`, live and retained, with
      topic name `orders/new`.
- [ ] A test: with that map, a subscriber to `#` receives nothing for a message published on canonical `orders/new`.
- [ ] A wildcard filter is mapped by the default rules alone, as a test shows.
- [ ] The `--topic-map` paragraph in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` no longer says the `mqtt`
      server does not apply a map, and lists it among those that do.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
