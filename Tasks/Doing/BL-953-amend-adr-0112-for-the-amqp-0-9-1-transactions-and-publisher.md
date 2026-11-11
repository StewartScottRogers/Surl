---
id: BL-953
title: Amend ADR-0112 for the AMQP 0-9-1 transactions and publisher confirms BL-731 serves
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-731]
touches: [Documentation/Planning/Decisions/ADR-0112-how-the-amqp-0-9-1-broker-answers-amqp-and-amqps.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-953 — Amend ADR-0112 for the AMQP 0-9-1 transactions and publisher confirms BL-731 serves

## Goal

ADR-0112 states what the broker now does for `tx` and `confirm` (BL-731) instead of "540 until BL-731".

## Context

- BL-731 served the `tx` class and RabbitMQ's `confirm.select` in
  `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary` (`Amqp0BrokerConnection.Transactions.cs`,
  `Amqp0Transaction`; the `Amqp0Broker` remarks list every answer). It could not edit the ADR because
  `Documentation/Planning/Decisions` was in BL-701's `touches` while it ran.
- The behaviour to record, decided by Claude under Stewart's delegation in BL-731 (see its Notes):
  - A transacted channel buffers its whole publishes and its `ack`/`reject`/`nack` in order; `commit` routes and
    settles them (returns for `mandatory` are sent then), `rollback` or the channel's close discards them and
    the settled deliveries are outstanding again. A tag not outstanding is 406 at once. Buffered bodies count
    toward `--max-filesize` with the content in progress (506 above it). `select` twice is answered twice.
  - Confirm mode numbers publishes from 1 and answers each with its own `basic.ack`, `multiple` clear, because
    each publish is routed before the next frame is read, so there is never more than one to confirm. A publish
    the bus refuses for a bound (`StoreFull`, `TooManyTopics`, and a persistent message to a full queue - the
    rows that are 506 outside confirm mode) is `basic.nack`, `multiple` and `requeue` clear, and the channel
    stays open; 403 and 311 stay channel exceptions. An unroutable publish is acked (after its `basic.return`).
  - `tx.select` in confirm mode and `confirm.select` on a transacted channel are channel exception 406.
  - `publisher_confirms` is in `connection.start`'s capabilities.

## Acceptance criteria

- [ ] ADR-0112 decision 6 replaces the "Transactions ... are BL-731's" bullet with the behaviour above, its
      `Succeeded` row no longer says publishes are unacknowledged outside a pending task, and decision 2's
      `server-properties` row lists `publisher_confirms` unconditionally.
- [ ] ADR-0112 decision 7's `540` row no longer lists `tx` and `confirm`, and a `406` row (or its text) names
      `commit`/`rollback` on a channel not transacted and mixing `tx` with `confirm`.
- [ ] The amendment is marked "Decided by Claude under Stewart's delegation" and dated.

## Notes

- Filed by BL-731.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
