---
id: BL-2110
title: Limit the AMQP 1.0 rollback and filter conformance rows to the topic links they cover
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2110 — Limit the AMQP 1.0 rollback and filter conformance rows to the topic links they cover

## Goal

The `Amqp1:Transactions-4.4.3-1` and `Amqp1:Surl-Filters-1` conformance rows say which topic links and deliveries
they cover.

## Context

- Found while working BL-2088 (2026-11-28), in `Documentation/Conformance/Extended/Amqp1.md`.
- `Amqp1:Transactions-4.4.3-1` says "a rollback returns those deliveries, unless retired outside the transaction, to
  their queue marked redelivered". A live topic subscription's delivery has no queue delivery tag: `RollBack` in
  `Amqp1ContainerConnection.Transactions.cs` only removes it from the unsettled set.
- `Amqp1:Surl-Filters-1` says "On a topic link a filtered-out message never counts against the 1000 waiting; on a
  queue link it stays ready". The first clause holds for a live topic subscription's link only. A topic link on a
  kept or non-durable shared subscription's queue (ADR-0146 decision 3 and Amendment 4) has nothing waiting on it and
  acknowledges a filtered-out message (`Amqp1ContainerConnection.Deliveries.cs`; ADR-0107, the BL-1990 note's
  "Filters" bullet).
- Documents only; no behaviour change.

## Acceptance criteria

- [ ] The `Amqp1:Transactions-4.4.3-1` row says only queue deliveries, a kept or non-durable shared subscription's included, go back to their queue, and a live topic subscription's do not.
- [ ] The `Amqp1:Surl-Filters-1` row says what happens to a filtered-out message on a live topic link, on a subscription queue's link and on a queue node's link.
- [ ] The diff changes only those two rows under `Documentation/`.

## Notes

## Log

- 2026-11-28: Created.
