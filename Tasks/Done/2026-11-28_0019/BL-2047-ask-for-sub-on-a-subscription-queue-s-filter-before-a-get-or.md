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
completed: 2026-11-27
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

- [x] `Surl.MessageBus.UnitTests`: with grants configured, an account with `$queue` rights but no `sub` on a described queue's filter gets `AccessDenied` from `GetFromQueue` and `Consume`, and the messages stay.
- [x] `Surl.MessageBus.UnitTests`: `ConsumeSubscriptionQueue` and a get and consume of a queue with no description behave as before.
- [x] `Surl.MessageBus.UnitLibrary/CLAUDE.md` states the rule, and its sentence saying the two ask only for the `$queue` grant is gone.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Decided (ADR-0146, Amendment 5): a get or consume by name of a described queue needs both the `$queue/<name>` grant
  and `sub` on every filter it is bound to. An exact `$queue/<name>` grant is no exception: telling it from a wildcard
  one would be a second rule, and secure by default asks for the filter's right.
- `MayRemoveMessages` is now `SubscriptionFiltersAllow`, since get and consume ask it too; `AccessMessages` is the
  lookup that asks it, used by get, consume and purge.
- The change is small, so the lane planned, tested and implemented it itself; `code-reviewer` reviewed it. No
  conformance run: upstream curl speaks none of these schemes.
- The review's one must-fix (the `MessageBusOutcome.AccessDenied` comment) and its two missing test cases are done.
  Filed BL-2051 (a restore can hand a described queue's messages to a consumer added before it was described) and
  BL-2052 (`--aihelp` text for `--topic-access`).
- Gates: build 0 warnings; fast tests 30564 passed, 0 failed, 3 skipped (656 in `Surl.MessageBus.UnitTests`);
  `Surl.MessageBus.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. A get or consume by name of a subscription queue now needs sub on every filter it is bound to, beside the $queue grant
