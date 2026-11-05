---
id: BL-630
title: Map MQTT, AMQP, Kafka, NATS and STOMP names onto the Surl.MessageBus topic space
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-629]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-630 — Map MQTT, AMQP, Kafka, NATS and STOMP names onto the Surl.MessageBus topic space

## Goal

`Surl.MessageBus` converts each messaging protocol's topic names and filters to and from the bus's canonical
topic space exactly as BL-622's ADR decides, so every broker uses one mapping instead of its own.

## Context

- Decision: BL-622's ADR, decision 3's mapping table. The names to map: MQTT topic names and filters (`a/b`,
  `+`, `#`, `$`-topics; MQTT 3.1.1 section 4.7, MQTT 5.0 section 4.7, and `$share/` per section 4.8.2), NATS
  subjects (`a.b`, `*`, `>`), AMQP 0-9-1 topic-exchange binding and routing keys (`a.b`, `*`, `#`) and queue
  names, AMQP 1.0 link addresses, Kafka topic names (and partition), STOMP `/topic/…` and `/queue/…`
  destinations.
- The library is BL-629's. Put the mappings beside the topic space so the brokers (`Surl.Protocol.Mqtt`, and the
  planned `Surl.Extended.Protocol.Mqtt5`, `Amqp0`, `Amqp1`, `Kafka`, `Nats`, `Stomp`) all call the same code;
  none of them may reference another (ADR-0002, ADR-0083 "Isolation").
- Every expected value comes from the specification text the ADR cites, never from the Curl port (ADR-0083
  decision 1).

## Acceptance criteria

- [ ] For each protocol the ADR maps, named tests show a name and a filter converted to the canonical form and
      back, the wildcard of one protocol matching what the same filter matches in another (e.g. MQTT `a/+/c` and
      NATS `a.*.c`), and each name the ADR says does not survive a mapping returning the ADR's typed refusal.
- [ ] A test publishes under an MQTT name and receives it through a subscription made with each other
      protocol's equivalent filter.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
