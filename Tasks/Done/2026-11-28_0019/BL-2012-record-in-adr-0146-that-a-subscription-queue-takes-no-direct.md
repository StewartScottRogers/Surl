---
id: BL-2012
title: Record in ADR-0146 that a subscription queue takes no direct send
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2012 — Record in ADR-0146 that a subscription queue takes no direct send

## Goal

ADR-0146 states that a queue carrying a subscription description takes no direct send, and why.

## Context

- Decided while working BL-1992 (2026-11-27, Claude under Stewart's delegation): once `sub` on the topic filter
  alone admits a subscription, a direct send into its queue (AMQP 0-9-1 default, direct, fanout or headers
  exchange, STOMP `/queue/`) would hand the subscriber messages that right does not cover. `PublishToQueue` and a
  batch's `ToQueue` entry therefore answer `UnknownQueue` for a described queue, as decision 3.4's
  `amqp:not-found` does for `/queue/surl-subscription-...`. `SeedEnqueue` is unchanged.
- BL-1992 could not edit the ADR: BL-1983, in `Doing` then, held `Documentation/Planning/Decisions`.
- The code is `MessageBus.DirectSendTarget` in `Protocols/Messaging/Surl.MessageBus.UnitLibrary`.

## Acceptance criteria

- [x] ADR-0146 has a dated amendment, marked "Decided by Claude under Stewart's delegation", stating the rule, the outcome answered, that seeding is unchanged, and the alternative rejected (checking each message's topic at consume time).

## Notes

- Added as ADR-0146 Amendment 6; its claims were checked against `MessageBus`, the AMQP 0-9-1, STOMP and
  AMQP 1.0 send paths. `dotnet build` clean, fast tests green (2026-11-27).
- The Context's exchange list was short: every exchange but `amq.topic` takes the direct-send path, declared
  topic exchanges included. The amendment says so.
- Context and BL-1992 named the rejected alternative without a reason; the reason written (a direct send's topic
  is the sender's own unjudged label) is Claude's, under the delegation.
- STOMP answering a described queue with `Resource limit exceeded` follows ADR-0100's table; left as it is.
- Other documents that need the rule are BL-2054.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. ADR-0146 Amendment 6 records that a subscription queue takes no direct send, why, and what each protocol's client is answered
