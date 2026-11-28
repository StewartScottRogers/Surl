---
id: BL-2114
title: Keep an undeliverable-here AMQP 1.0 delivery from its link when its transaction rolls back
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2114 — Keep an undeliverable-here AMQP 1.0 delivery from its link when its transaction rolls back

## Goal

A delivery acquired in a transaction and answered `modified` with `undeliverable-here` outside it is not offered to
the same link again after the transaction rolls back.

## Context

- Found by reading the code while working BL-2088 (2026-11-28). No test has run it, so confirm it first.
- `ApplyOutcome` (`Amqp1ContainerConnection.Deliveries.cs`) leaves the queue delivery tag unacknowledged when the
  state is `modified` with `undeliverable-here`. Under `rcv-settle-mode` `first` an unsettled `disposition` leaves the
  delivery in the unsettled set.
- `RollBack` (`Amqp1ContainerConnection.Transactions.cs`) then calls `Consumer.Reject(tag, requeue: true)` for it,
  which could offer the message to the same link again.
- Conformance row `Amqp1:Messaging-3.4.5-1` and ADR-0107 ("The client's outcome") say such a message is never
  redelivered to the modifying link and goes back to the queue when the link ends. Read AMQP 1.0 Part 4 section 4.4.3
  and ADR-0146 decision 1.5 and Amendment 1 for what a rollback must do; record the answer in an ADR if they leave it
  open.

## Acceptance criteria

- [x] A test in `Surl.Extended.Protocol.Amqp1.UnitTests` acquires a queue delivery in a transaction, states `modified` with `undeliverable-here` true unsettled and outside it, rolls back, and asserts the link is not sent that message again.
- [x] A test asserts that the same message goes back to the queue, marked redelivered, when that link ends.
- [x] If the specification requires different behaviour, an ADR records it and the `Amqp1:Messaging-3.4.5-1` row states it.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Defect confirmed: the two new rollback tests failed before the fix (the link was sent the message again).
- Spec: Part 3 section 3.4.5 forbids redelivery to the modifying link. Part 4 section 4.4.4 says an outcome for an
  acquired delivery "MUST be associated with the same transaction" and states no rule for a client that sends one
  outside it, so it is left open. Decided in ADR-0146 Amendment 10: a rollback leaves a delivery with an applied
  outcome as it is. `accepted` and the other outcomes left unsettled so also stay counted, where they were removed.
- Review found a second route: a later transaction's commit reapplied an outcome over `undeliverable-here` and
  requeued the delivery. `CommitOutcomesAsync` now applies only a delivery's first outcome; its test failed first.
- Pipeline: planning, tests, implementation and documents were done in the lane's own session (a few lines in one
  library); review was delegated to `code-reviewer`. No conformance-auditor run: an extended server upstream curl
  cannot speak to (ADR-0083).
- `touches` gained the ADR-0146 file; no task in Doing named `Documentation/Planning/Decisions`.
- Filed BL-2127: a rollback still requeues a delivery whose outcome another open transaction holds.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. A transaction rollback or a later commit no longer redelivers an undeliverable-here AMQP 1.0 delivery to its link (ADR-0146 Amendment 10)
