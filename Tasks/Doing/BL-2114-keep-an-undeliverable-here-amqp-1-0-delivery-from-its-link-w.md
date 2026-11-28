---
id: BL-2114
title: Keep an undeliverable-here AMQP 1.0 delivery from its link when its transaction rolls back
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-28
completed:
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

- [ ] A test in `Surl.Extended.Protocol.Amqp1.UnitTests` acquires a queue delivery in a transaction, states `modified` with `undeliverable-here` true unsettled and outside it, rolls back, and asserts the link is not sent that message again.
- [ ] A test asserts that the same message goes back to the queue, marked redelivered, when that link ends.
- [ ] If the specification requires different behaviour, an ADR records it and the `Amqp1:Messaging-3.4.5-1` row states it.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
