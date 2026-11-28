---
id: BL-1996
title: Record in ADR-0146 that a subscription queue takes no direct send
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1996 — Record in ADR-0146 that a subscription queue takes no direct send

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

- [ ] ADR-0146 has a dated amendment, marked "Decided by Claude under Stewart's delegation", stating the rule, the outcome answered, that seeding is unchanged, and the alternative rejected (checking each message's topic at consume time).

## Notes

## Log

- 2026-11-27: Created.
