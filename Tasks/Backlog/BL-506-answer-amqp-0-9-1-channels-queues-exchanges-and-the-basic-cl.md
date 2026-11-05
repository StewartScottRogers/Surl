---
id: BL-506
title: Answer AMQP 0-9-1 channels, queues, exchanges and the basic class
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-505, BL-664, BL-706, BL-730]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-506 — Answer AMQP 0-9-1 channels, queues, exchanges and the basic class

## Goal

The AMQP 0-9-1 broker opens and closes channels, declares queues and exchanges, binds them, and routes
`basic.publish` (method, content header and body frames) to queues. It answers `basic.get` with `get-ok` or
`get-empty`, returns unroutable mandatory messages with `basic.return`, and serves acknowledgements and consumers
as BL-503's ADR decides. Each misuse is a channel or connection exception with its reply code.

## Context

- Decision: BL-503's ADR, decisions 5, 6, 7 and 8.
- AMQP 0-9-1: section 1.5 (channel), 1.6 (exchange), 1.7 (queue), 1.8 (basic), 3.1.3 (the default exchange binds
  every queue by its name), and 4.2.6 (a content header's body size, and body frames until it is reached). Reply
  codes `404` NOT_FOUND, `405` RESOURCE_LOCKED, `406` PRECONDITION_FAILED and `312` NO_ROUTE (`basic.return`).
- Broker state (queues and exchanges) is shared by connections and owned by the server instance, not a static.
- **Shared message bus (Stewart, 2026-11-04; BL-622's ADR, applied by BL-503's ADR):** queues are the bus's
  queues (BL-631) and exchange routing maps onto its topic space with BL-630's mapping, so a message published over
  `mqtt` reaches a queue bound on `amq.topic`, and one published here reaches `mqtt` subscribers. The library
  references `Surl.MessageBus.UnitLibrary`. Each publish, declare, bind and consume passes the connection's account
  and scheme to BL-664's access check, a refusal answered `403` ACCESS_REFUSED as BL-503's ADR says.

## Acceptance criteria

- [ ] Named tests show:
      - a message published to the default exchange and fetched with `basic.get`, in three body frames;
      - `get-empty` on an empty queue;
      - a mandatory unroutable message returned with `312`;
      - a passive declare of a missing queue gets `404`;
      - re-declaring with different flags gets `406`;
      - an exclusive queue used from another connection gets `405`;
      - `basic.ack` of an unknown delivery tag gets `406`;
      - a server-named queue;
      - consumers delivered in order, if the ADR serves `basic.consume`;
      - a message published on the bus under an MQTT name delivered to a queue bound on `amq.topic` with the mapped
        key;
      - a publish or consume refused by topic access answered as the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-05 (BL-503): ADR-0112 decides this task's behaviour. It needs BL-730's bus queue operations (passive inspect, purge, single get, conditional delete, exclusive consumers, immediate publish, consumer stop, two priority levels) and BL-706's native form (`amqp0`). Transactions and publisher confirms are BL-731's, the headers exchange and durable exchanges across restarts BL-732's; the matrix `Documentation/Conformance/Extended/Amqp0.md` lists the rows this task proves.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-664; queues and routing are Surl.MessageBus's.
- 2026-11-05: Waits on BL-706 and BL-730 too (BL-503, ADR-0112).
