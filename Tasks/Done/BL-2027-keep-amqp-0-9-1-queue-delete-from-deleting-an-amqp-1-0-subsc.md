---
id: BL-2027
title: Keep AMQP 0-9-1 queue.delete from deleting an AMQP 1.0 subscription queue without the sub right
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md, Documentation/Planning/Decisions/ADR-0112-how-the-amqp-0-9-1-broker-answers-amqp-and-amqps.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2027 — Keep AMQP 0-9-1 queue.delete from deleting an AMQP 1.0 subscription queue without the sub right

## Goal

An account without the `sub` right on a subscription queue's topic filter cannot delete or empty that queue.

## Context

- Found by the code review of BL-2010 (2026-11-27), not run. BL-2010 covers only the AMQP 1.0 container's deletes.
- `BusConnection.DeleteQueue` and `PurgeQueue` judge no grant, and AMQP 0-9-1 `queue.delete` calls the former
  (`Amqp0BrokerConnection.Exchanges.cs`). A client that knows or derives a `surl-subscription-` name
  (`Amqp1KeptSubscription.QueueNameFor` hashes the container id and link name) can remove another account's
  persisted subscription. BL-2011 covers only bind and unbind.
- A likely rule: on a queue carrying a subscription description, both need `sub` on every filter it is bound to
  (`AccessDenied` otherwise); a queue with none behaves as before. `DeleteQueueIfDescribed` must keep working for the
  AMQP 1.0 container, including its deletes at a session's end. Record the rule in the library's `CLAUDE.md`.

## Acceptance criteria

- [x] `Surl.MessageBus.UnitTests`: with grants configured, an account without `sub` on a described queue's filter gets `AccessDenied` from `DeleteQueue` and `PurgeQueue`, and the queue and its messages stay.
- [x] `Surl.MessageBus.UnitTests`: an account with the right deletes and purges it, and a queue with no description is deleted and purged as before.
- [x] `Surl.MessageBus.UnitLibrary/CLAUDE.md` states the rule.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Decided (ADR-0146 Amendment 2): delete and purge of a described queue need `sub` on every bound filter. Bound
  to none, they are refused whenever any grant is configured (secure by default: nothing to judge, and an unbind
  asks no grant). No grants: everyone may. `DeleteQueueIfDescribed` stays grantless.
- Added ADR-0146 and ADR-0112 to `touches` to record the decision and the new `403`; no task in Doing named either.
- Not covered: `Consume` and `GetFromQueue` still ask only for the `$queue` grant, so a caller holding it can drain
  a subscription queue. Filed as BL-2028. Swapping bindings before a delete is BL-2011.
- No Amqp0 broker test for the new `403`: that project is outside `touches`, and the broker's existing
  `AccessDenied` to `403` mapping carries it. Plan, tests and implementation were done in the session; the
  review stage ran `code-reviewer`. No upstream curl run: curl does not speak AMQP.
- Gates: build 0 warnings, format clean, fast tests green, `Surl.MessageBus.UnitLibrary` 100% line and branch,
  0 failing members.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. AMQP 0-9-1 queue.delete and queue.purge on a subscription queue are refused without sub on its filters
