---
id: BL-731
title: Serve AMQP 0-9-1 transactions and publisher confirms
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-506]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-731 — Serve AMQP 0-9-1 transactions and publisher confirms

## Goal

The AMQP 0-9-1 broker serves the `tx` class (`select`, `commit`, `rollback`) and RabbitMQ's `confirm.select` with
publisher `basic.ack`/`basic.nack`, and announces `publisher_confirms` in `connection.start`.

## Context

- Decision: ADR-0112 decision 6 ("Transactions ... and publisher confirms are BL-724's"), decision 2's
  capabilities and decision 7's table (`540` until this task; `406` for misuse). Matrix rows `Amqp0:Tx-1` and
  `Amqp0:Extension-3` in `Documentation/Conformance/Extended/Amqp0.md`.
- AMQP 0-9-1 XML class `tx` (90): `select`/`select-ok`, `commit`/`commit-ok`, `rollback`/`rollback-ok`; rules
  `commit`/`transacted` and `rollback`/`transacted` (406 on a channel not transacted); the class rules that
  clients must not rely on atomicity across queues, `mandatory` or `immediate`; `basic.ack` rule `exists` (a bad
  tag is refused at once on a transacted channel, not at commit).
- RabbitMQ's extensions (<https://www.rabbitmq.com/docs/specification>, `amqp0-9-1.extended.xml`): `confirm`
  class 85, `select` (10, with `nowait`) and `select-ok` (11); in confirm mode each publish gets a sequence number
  from 1 and the broker sends `basic.ack` (or `basic.nack` when the bus refuses it - ADR-0112 decision 6's
  `StoreFull`/`TooManyTopics` rows become a `nack` there), `multiple` allowed. A channel cannot be both
  transacted and in confirm mode (406).
- Transactions are the broker's own buffer per channel: publishes and acknowledgements are applied in order at
  `commit`, discarded at `rollback`; the buffer counts toward the connection's `--max-filesize` bound (ADR-0112
  decision 8).

## Acceptance criteria

- [ ] Named tests show: publishes on a transacted channel invisible until `commit` and gone after `rollback`; an
      acknowledgement taking effect only at `commit`; `commit` on a channel not transacted answered `406`; confirm
      mode acknowledging each publish in order, `multiple` included; a publish the bus refuses `nack`ed in confirm
      mode; `tx.select` on a channel in confirm mode (and the reverse) answered `406`; `publisher_confirms` in
      `connection.start`'s capabilities.
- [ ] `Documentation/Conformance/Extended/Amqp0.md`'s rows `Amqp0:Tx-1` and `Amqp0:Extension-3` name the tests,
      and ADR-0112 decision 7's `540` row no longer lists `tx` and `confirm`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-503 (ADR-0112), split from BL-506 so each task is one run.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
