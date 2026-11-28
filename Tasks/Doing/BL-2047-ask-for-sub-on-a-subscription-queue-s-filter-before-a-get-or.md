---
id: BL-2047
title: Ask for sub on a subscription queue's filter before a get or consume by name takes its messages
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2047 — Ask for sub on a subscription queue's filter before a get or consume by name takes its messages

## Goal

An account without the `sub` right on a subscription queue's topic filter cannot take that queue's messages by name.

## Context

- Found by the code review of BL-2027 (2026-11-27), read in the code, not run.
- `MessageBus.GetFromQueue` and `Consume` (`MessageBus.Queues.cs`) ask only for the `$queue/<name>` grant, also on a
  queue carrying a subscription description. A caller with `sub` on `$queue/#` can drain it: AMQP 0-9-1 `basic.get`,
  `basic.consume` with `no-ack`, `basic.reject` without requeue, and STOMP `SUBSCRIBE /queue/surl-subscription-...`.
  AMQP 1.0 refuses that address prefix.
- A likely rule: on a described queue both also need what BL-2027's `MayRemoveMessages` asks (ADR-0146 Amendment 2),
  under the lock; `ConsumeSubscriptionQueue` stays as it is. If an explicit `$queue` grant should allow it instead,
  decide so in a further amendment.

## Acceptance criteria

- [ ] `Surl.MessageBus.UnitTests`: with grants configured, an account with `$queue` rights but no `sub` on a described queue's filter gets `AccessDenied` from `GetFromQueue` and `Consume`, and the messages stay.
- [ ] `Surl.MessageBus.UnitTests`: `ConsumeSubscriptionQueue` and a get and consume of a queue with no description behave as before.
- [ ] `Surl.MessageBus.UnitLibrary/CLAUDE.md` states the rule, and its sentence saying the two ask only for the `$queue` grant is gone.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
