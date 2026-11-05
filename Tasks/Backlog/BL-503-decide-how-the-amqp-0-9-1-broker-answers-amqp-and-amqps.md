---
id: BL-503
title: Decide how the AMQP 0-9-1 broker answers amqp and amqps
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-503 — Decide how the AMQP 0-9-1 broker answers amqp and amqps

## Goal

An ADR decides how `surl amqp://` and `amqps://` answer as an AMQP 0-9-1 broker. It covers the protocol header,
the connection negotiation, authentication, virtual hosts, the exchanges and queues, the classes and methods
served, heartbeats, the exceptions and limits. A server-side requirement matrix traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specification: AMQP 0-9-1 (2008-11-13,
  https://www.rabbitmq.com/resources/specs/amqp0-9-1.pdf) and its XML protocol definition `amqp0-9-1.xml`
  (class and method IDs, fields, and the reply-code constants), with RabbitMQ's errata
  (https://www.rabbitmq.com/amqp-0-9-1-errata.html) for the field-table types.
- Which methods a client sends (what to accept, not a verdict): the port's `Curl.Extended.Protocol.Amqp0.UnitLibrary`
  and its matrices `Amqp0.md` and `Amqp0-Messaging.md`. The port's `Amqp0ReferenceBroker` is copyable code.

Decide and record:

1. The protocol header check: an unsupported header is answered with `AMQP 0 0 9 1` and closed (section 4.2.2).
2. `connection.start`: the mechanisms (`PLAIN`, `AMQPLAIN`; `EXTERNAL` only with client certificates, if at all),
   the server properties, the locales; the accounts check, and the reply for a refused login (403
   ACCESS_REFUSED, then close).
3. `connection.tune`: channel-max, frame-max and heartbeat, and the heartbeat timeout on an injected `TimeProvider`.
4. Virtual hosts: `/` only, or one per accounts entry.
5. The exchanges (the default exchange, `amq.direct`, `amq.fanout`, `amq.topic`, and `exchange.declare`) and the
   queue semantics (`queue.declare` flags, server-named queues, exclusive and auto-delete).
6. The basic class methods served (`publish`, `get`, `ack`, `reject`, `nack`, `consume`/`deliver`/`cancel`,
   `qos`, `recover`), `mandatory` with `basic.return`, and whether messages survive a restart.
7. Channel and connection exceptions, with the reply code for each failure.
8. `amqps` over TLS, and each ADR-0006 limit (frame-max, channels, queues, message size).

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 8, each citing its specification section, and
      is listed in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/Amqp0.md` exists in the matrix format, each "covered by" naming
      BL-504, BL-505 or BL-506, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
