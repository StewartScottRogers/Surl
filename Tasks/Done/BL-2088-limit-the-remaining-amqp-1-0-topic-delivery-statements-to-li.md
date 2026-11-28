---
id: BL-2088
title: Limit the remaining AMQP 1.0 topic delivery statements to live subscriptions
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1Link.cs, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1ContainerConnection.Transactions.cs, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2088 — Limit the remaining AMQP 1.0 topic delivery statements to live subscriptions

## Goal

The AMQP 1.0 link and transaction comments and the `Amqp1:Surl-Delivery-1` conformance row say which topic
deliveries they mean: a live subscription's, not a kept or non-durable shared one's.

## Context

- Found while working BL-2062 (2026-11-28). A topic link on a subscription's bus queue (a kept subscription's or a
  non-durable shared one's, ADR-0146 decision 3 and Amendment 4) consumes a queue: it has `Amqp1Link.Consumer` and no
  `Amqp1Link.Subscription`, and its deliveries carry a queue delivery tag.
- In `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1Link.cs`: `Subscription` says "When the
  container sends from a topic: the live subscription"; `Consumer` says "from a queue" without naming subscription
  queues; `DroppedCount` says "topic messages were dropped", which happens to a live subscription's only.
- In `Amqp1ContainerConnection.Transactions.cs`, the comment above `RollBack` says acquired deliveries not retired
  outside the transaction "go back to their queue, redelivered"; a live topic delivery has no tag and is only removed
  from the unsettled set.
- `Documentation/Conformance/Extended/Amqp1.md`, row `Amqp1:Surl-Delivery-1`: "Deliveries from a topic wait on the
  link ... every outcome only settles them" holds for a live subscription only.
- BL-2045 corrects other files of this library; do not edit those here.
- Documents and comments only; no behaviour change.

## Acceptance criteria

- [x] The XML comments on `Amqp1Link.Subscription`, `Consumer` and `DroppedCount` say that a kept or non-durable shared subscription uses `Consumer`, and that only a live subscription's messages are dropped and counted.
- [x] The comment above `RollBack` says only deliveries with a queue delivery tag go back to their queue.
- [x] The `Amqp1:Surl-Delivery-1` row says it covers deliveries from a live topic subscription.
- [x] The diff changes only comment lines in the two `.cs` files and that one row, `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Delivered by `align-and-document`. The `RollBack` comment also names two conditions the code tests: a settled held
  outcome is unsettled again only while its link has not ended, and a delivery already settled on the bus by an outcome
  stated outside the transaction does not go back (ADR-0146 Amendment 1).
- Verified 2026-11-28: `dotnet build -warnaserror` 0 warnings; `dotnet format --verify-no-changes` clean; fast tests
  passed in every test project. No Integration run: only comments and one document row changed.
- Filed with the board script, not `task-planner`: BL-2113 (two more conformance rows) and BL-2114 (a possible
  rollback defect with `undeliverable-here`, read from the code, not yet reproduced).

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. AMQP 1.0 link and rollback comments and the Surl-Delivery-1 row now say which topic deliveries they mean
