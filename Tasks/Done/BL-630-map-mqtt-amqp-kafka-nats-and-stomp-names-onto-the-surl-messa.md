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
completed: 2026-11-04
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

- [x] For each protocol the ADR maps, named tests show a name and a filter converted to the canonical form and
      back, the wildcard of one protocol matching what the same filter matches in another (e.g. MQTT `a/+/c` and
      NATS `a.*.c`), and each name the ADR says does not survive a mapping returning the ADR's typed refusal.
- [x] A test publishes under an MQTT name and receives it through a subscription made with each other
      protocol's equivalent filter.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Shape: one static class per protocol beside `CanonicalTopicName` - `MqttTopicNames`, `NatsSubjectNames`,
  `AmqpTopicExchangeNames`, `KafkaTopicNames`, `Amqp1AddressNames`, `StompDestinationNames` - each with
  `ToCanonical…` (inbound) and `FromCanonical…`/`To…Address` (outbound). Results are `TopicNameMapping` or
  `BusDestinationMapping`; the typed refusal is `TopicNameMappingOutcome`: `InvalidName` (inbound, the server's own
  error: NATS `-ERR`, AMQP unroutable, Kafka 17), `Unreachable` (outbound: a canonical topic the protocol cannot
  name, so the server sends nothing - this is how "a message published on it is not delivered to them" holds,
  since e.g. NATS `a.*.c` = `a/+/c` also matches `a//c`), `UnknownDestination` (AMQP 1.0 `amqp:not-found`, STOMP
  `ERROR`), `UnknownPartition` (Kafka 3). Kafka's `INVALID_PARTITIONS` (37) belongs to CreateTopics in the Kafka
  broker, not to name mapping.
- Decided (default taken): a token or word is a wildcard only when it is exactly `*`, `>` or `#`; `a*b` is literal
  in NATS and AMQP 0-9-1, as their servers treat it. NATS tokens also refuse white space (` `, tab, CR, LF).
- Decided (default taken): AMQP 0-9-1 empty words and an empty routing key are refused (`InvalidName`, unroutable),
  per the ADR. A literal canonical level `*` is unreachable as a binding-key word and as a NATS token (it would
  become a wildcard), but `*` is a fine literal routing-key word.
- Decided by each protocol's specification, against the ADR's literal text (ADR folder held by BL-642, so filed
  as BL-691 to amend ADR-0100): NATS's last `>` becomes `+/#` (one or more levels), not `#` (which would also
  match `a` for `a.>`); Kafka names with empty levels (`a..b`) map to `a//b` and back, only `.`/`..` refused.
- AMQP 0-9-1 queue names need no code: a queue name is the bus queue's name (BL-631 owns queues). AMQP 1.0 and
  STOMP `/queue/<q>` require a non-empty `<q>` without U+0000. Kafka has no wire wildcards, so its "filter" is the
  topic itself. ADR-0100 3.10's `ITopicNameLookup` seam is not in this task's acceptance; left to layer 2.
- Method-group delegates are held in `static readonly` fields: a per-call-site delegate cache is a branch that
  parallel test starts can leave half-covered.
- Filed: BL-691 (amend ADR-0100 decision 3.2 for the NATS and Kafka points).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.MessageBus maps MQTT, MQTT 5 shared, NATS, AMQP 0-9-1, AMQP 1.0, STOMP and Kafka names onto the canonical topic space and back, with typed refusals
