---
id: BL-2087
title: Correct the AMQP 1.0 library's outcome reading, topic delivery and subscription access order statements
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/CLAUDE.md, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1DeliveryState.cs, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1ContainerConnection.Deliveries.cs]
requirement: none
created: 2026-11-28
completed:
---
# BL-2087 — Correct the AMQP 1.0 library's outcome reading, topic delivery and subscription access order statements

## Goal

The AMQP 1.0 library's notes and comments say which delivery states read as an outcome, which topic deliveries
have nothing to put back, and the order a subscription attach inspects and judges in.

## Context

Found while working BL-2045 (2026-11-28). All paths are in
`Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary`.

- `CLAUDE.md`, the type system section: "`Amqp1DeliveryState` ... reads each delivery state as an
  `Amqp1Outcome`". `declared` and `transactional-state` read as `Amqp1Outcome.None`.
- `Amqp1DeliveryState.cs`, the `<returns>` of `OutcomeOf`: "`None` when absent or not a delivery state". It is
  also `None` for `declared` and `transactional-state`.
- `Amqp1ContainerConnection.Deliveries.cs`, the comment on `ApplyOutcome`: "A topic delivery has nothing to put
  back". That holds for a live subscription only; a subscription queue's delivery has a `QueueDeliveryTag`.
- `CLAUDE.md`, durable and shared topic subscriptions, "Who may": "Each attach judges it first ..., then inspects
  the queue". `OpenSubscriptionQueue` calls `InspectQueue` before `Judge` (and `FindKeptSubscription` inspects
  for an attach with no source); nothing changes before the judgement.
- Describe the code as it is when this task runs. Comments and notes only.

## Acceptance criteria

- [ ] The `CLAUDE.md` type system section says which delivery states read as an `Amqp1Outcome` other than `None`.
- [ ] The `<returns>` of `OutcomeOf` says it is `None` for `declared` and `transactional-state` too.
- [ ] The `ApplyOutcome` comment limits "nothing to put back" to a live subscription's delivery.
- [ ] The "Who may" statement gives the order the code calls `InspectQueue` and `Judge` in.
- [ ] The diff changes only comments in the two `.cs` files and text in `CLAUDE.md`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
