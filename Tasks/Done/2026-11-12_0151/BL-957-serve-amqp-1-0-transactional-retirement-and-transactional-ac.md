---
id: BL-957
title: Serve AMQP 1.0 transactional retirement and transactional acquisition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-956]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
# BL-957 — Serve AMQP 1.0 transactional retirement and transactional acquisition

## Goal

A client's `disposition` in a transaction takes effect only when the transaction commits, and deliveries a link
acquires in a transaction return to their queue when it rolls back.

## Context

- ADR-0146 decision 1.5 (and 1.6 and 1.7 for what commit, rollback and ending do to them); AMQP 1.0 Part 4
  sections 4.4.2, 4.4.3 and 4.4.4.
- BL-956 builds the transactions, the coordinator and the held messages; this task adds the held outcomes and
  the `txn-id` in a `flow`'s `properties`.
- Rollback leaves a retired delivery acquired and unsettled again, as AMQP 0-9-1's rollback puts deliveries back
  outstanding (ADR-0112 decision 6).
- Matrix rows: `Amqp1:Transactions-4.4.2-1`, `-4.4.3-1`, `-4.4.1-2` (disposition) and the BL-957 part of
  `Amqp1:Surl-Transactions-1`.

## Acceptance criteria

- [x] A `disposition` in `transactional-state` with `accepted` leaves the queue delivery unacknowledged until a
      commit acknowledges it; a test shows the same for `rejected`, `released` and `modified` at commit.
- [x] After a rollback a retired delivery is unsettled on its link again, and goes back to its queue when the
      link ends.
- [x] After a `flow` carrying `txn-id`, the container's transfers carry `transactional-state` naming it; a
      `flow` without it stops that; a rollback returns those deliveries not retired to their queue marked
      redelivered.
- [x] A `disposition` naming an unknown `txn-id` is answered `rejected` with `amqp:transaction:unknown-id`.
- [x] Each row above names its tests in `Amqp1.md`; 100% line and branch coverage holds.

## Notes

- Filed by BL-707 (ADR-0146 decision 1.5).

## Log

- 2026-11-06: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. AMQP 1.0 container holds transactional dispositions until commit and acquires deliveries in a transaction via flow txn-id
