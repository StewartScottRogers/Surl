---
id: BL-2027
title: Keep AMQP 0-9-1 queue.delete from deleting an AMQP 1.0 subscription queue without the sub right
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-27
completed:
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

- [ ] `Surl.MessageBus.UnitTests`: with grants configured, an account without `sub` on a described queue's filter gets `AccessDenied` from `DeleteQueue` and `PurgeQueue`, and the queue and its messages stay.
- [ ] `Surl.MessageBus.UnitTests`: an account with the right deletes and purges it, and a queue with no description is deleted and purged as before.
- [ ] `Surl.MessageBus.UnitLibrary/CLAUDE.md` states the rule.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
