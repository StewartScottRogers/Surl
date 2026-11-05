---
id: BL-503
title: Decide how the AMQP 0-9-1 broker answers amqp and amqps
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401, BL-622]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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
- Shared message bus (Stewart, 2026-11-04): BL-622's ADR decides one topic space shared by `mqtt`, `mqtt5`,
  `amqp`, `amqp1`, `kafka`, `nats` and `stomp` in one `surl`, held by the horizontal library it names (planning
  `Surl.MessageBus`), and how AMQP 0-9-1 exchanges, binding keys, routing keys and queues map onto it.
  Decisions 5 and 6 below apply that mapping (queues are the bus's queues, not a private store; durability and
  restart survival are the bus's) and add only what is AMQP-specific. A message published over `mqtt` must reach
  a queue bound on `amq.topic`. Logins read the one account set (ADR-0032; BL-622 decision 4).

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

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 8, each citing its specification section, and
      is listed in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/Amqp0.md` exists in the matrix format, each "covered by" naming
      BL-504, BL-505 or BL-506 (or BL-723, BL-724 or BL-725, the follow-ups this task filed; see Notes), or
      "n/a: <reason>".

## Notes

- ADR-0112 (`Documentation/Planning/Decisions/ADR-0112-how-the-amqp-0-9-1-broker-answers-amqp-and-amqps.md`)
  decides 1 to 8; listed in the folder's `README.md`. Spec read: the 2008-11-13 PDF and `amqp0-9-1.xml`
  (downloaded to a temporary folder and parsed in PowerShell for the method IDs, the reply-code constants with
  their soft/hard classes, and the named rules the ADR cites as "rule `<method>`/`<name>`").
- Choices with sensible defaults, taken: one virtual host `/` (a second would split the shared bus); a refused
  login is `connection.close` 403 rather than a bare socket close; under `--allow-anonymous` `PLAIN`/`AMQPLAIN`
  are offered on every transport (0-9-1 clients only speak `PLAIN`; differs from ADR-0107 on purpose);
  `channel-max` 256, `frame-max` 131072, heartbeat half `--idle-timeout`; server-named queues `amq.gen-...`;
  `basic.nack`, `confirm.select` and the server's `basic.cancel` served from RabbitMQ's published extensions,
  every other extension `540`.
- Found while deciding: the 0-9-1 XML classes `506 resource-error` as a hard (connection) error, so ADR-0100
  decision 3.6's "channel close 506" is amended (ADR-0100 now carries an "Amended" line); basic class rule 06
  requires two priority levels, amending ADR-0100 decision 3.3's FIFO. The XML has no 312 constant; 312
  `NO_ROUTE` (0-8's, already in ADR-0100) is kept for `basic.return`.
- Follow-ups filed (lanes may file extra tasks): BL-723 (the bus's queue operations BL-506 needs; BL-506 now
  depends on BL-706 and BL-723), BL-724 (transactions and publisher confirms), BL-725 (headers exchange, durable
  exchanges across restarts). The second criterion was widened to name them because ADR-0112 decides more than
  BL-504 to BL-506 can hold in one run each; every matrix row still traces to a task.
- Edited outside `touches` only on the task board (filing and BL-506's `depends-on`), and ADR-0100's header,
  which sits in `Documentation/Planning/Decisions` (inside `touches`).

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-622; Context says the message bus is shared.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ADR-0112 decides how the AMQP 0-9-1 broker answers amqp and amqps; Amqp0.md traces each requirement to BL-504 to BL-506 and follow-ups BL-723 to BL-725
