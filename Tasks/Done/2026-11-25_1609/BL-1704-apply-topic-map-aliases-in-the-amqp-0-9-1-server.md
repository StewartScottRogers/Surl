---
id: BL-1704
title: Apply --topic-map aliases in the AMQP 0-9-1 server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md, Documentation/Planning/Decisions/ADR-0112-how-the-amqp-0-9-1-broker-answers-amqp-and-amqps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1704 — Apply --topic-map aliases in the AMQP 0-9-1 server

## Goal

A `--topic-map` whose left side is `amqp:<routing key>` or `amqps:<routing key>` applies to AMQP 0-9-1 clients both
ways, as ADR-0102 decision 3.3 says.

## Context

- Found in BL-1703: the server maps names with `AmqpTopicExchangeNames` (the default rules) in
  `Amqp0BrokerConnection.Publishing.cs`, `Amqp0BrokerConnection.Exchanges.cs` and `Amqp0BusMessages.cs`, and never
  asks the bus's `MessageBus.Names` (`TopicNameLookup`), so a map naming `amqp` on its left is accepted at start and
  does nothing. ADR-0102 3.3's own example, `amqp:orders=mqtt:shop/orders`, is such a map.
- NATS, MQTT 5, AMQP 1.0, STOMP, Kafka and Redis already call `bus.Names`; follow
  `Surl.Extended.Protocol.Stomp.UnitLibrary/StompServerConnection.Routing.cs`.
- The expected behaviour comes from ADR-0102 3.3, its BL-1703 amendment and the AMQP 0-9-1 specification (ADR-0083).
- Stand-in: the map joins this server to the others through the one shared message store.

## Acceptance criteria

- [x] An `Surl.Extended.Protocol.Amqp0.UnitTests` test: with the map `amqp:orders=mqtt:shop/orders`, a publish to
      `amq.topic` with routing key `orders` lands on canonical `shop/orders`.
- [x] A test: a queue bound to `amq.topic` with key `orders` receives a message published on `shop/orders`, delivered
      with routing key `orders`.
- [x] A test: with that map, a queue bound to `#` does not receive routing key `orders` for a message published on
      canonical `orders`.
- [x] A wildcard binding key is mapped by the default rules alone, as a test shows.
- [x] The `--topic-map` paragraph in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` no longer says the `amqp`
      server does not apply a map, and lists it among those that do.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Plan: `Amqp0TopicNames` (new) asks `MessageBus.Names` for the listener's scheme; publish, `amq.topic` bind and
  unbind, `reply-to` both ways and outbound routing keys go through it. Behaviour recorded in ADR-0102's BL-1704
  amendment (decided by Claude under Stewart's delegation), with a pointer in ADR-0112.
- A hidden topic copied into a queue by a wildcard binding is delivered from the default exchange under the queue's
  name, as ADR-0112 decision 6 delivers any topic with no 0-9-1 name, rather than discarded: a queue also holds
  direct sends and the bus message does not say how it arrived.
- Pipeline: planned, tested (new tests failed first) and implemented in the session rather than by one agent per
  stage, to save tokens on a change of about 30 production lines; `code-reviewer` reviewed it unanchored. No
  conformance stage: upstream curl speaks neither scheme (ADR-0083).
- Review found two defects, both fixed with a failing test first: `*` is legal in a routing key, so a map named
  `orders.*` was applied to the wildcard binding key `orders.*`; and a map name past 255 bytes could not be written
  as a `shortstr`. The first fix is in `TopicNameLookup.ToCanonicalFilter`, the one place names are mapped, so
  `touches` gained `Surl.MessageBus` and its tests, plus the two ADRs. No task in Doing named any of them.
- Verified: build with `-warnaserror` clean, `dotnet format --verify-no-changes` clean, 28959 fast tests passed.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. --topic-map aliases naming amqp or amqps now apply both ways in the AMQP 0-9-1 server (routing keys, amq.topic binding keys, reply-to)
