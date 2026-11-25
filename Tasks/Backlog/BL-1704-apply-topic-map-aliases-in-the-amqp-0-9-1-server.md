---
id: BL-1704
title: Apply --topic-map aliases in the AMQP 0-9-1 server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed:
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

- [ ] An `Surl.Extended.Protocol.Amqp0.UnitTests` test: with the map `amqp:orders=mqtt:shop/orders`, a publish to
      `amq.topic` with routing key `orders` lands on canonical `shop/orders`.
- [ ] A test: a queue bound to `amq.topic` with key `orders` receives a message published on `shop/orders`, delivered
      with routing key `orders`.
- [ ] A test: with that map, a queue bound to `#` does not receive routing key `orders` for a message published on
      canonical `orders`.
- [ ] A wildcard binding key is mapped by the default rules alone, as a test shows.
- [ ] The `--topic-map` paragraph in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` no longer says the `amqp`
      server does not apply a map, and lists it among those that do.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
