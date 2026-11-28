---
id: BL-2010
title: Delete an AMQP 1.0 subscription queue only while it is still the subscription the link was judged on
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-2010 — Delete an AMQP 1.0 subscription queue only while it is still the subscription the link was judged on

## Goal

A link deletes a subscription queue only while that queue still carries the description the link was judged on, so
no account deletes a subscription whose topic filter it may not subscribe to.

## Context

- `Amqp1ContainerConnection.Subscriptions.cs` deletes by queue name with `BusConnection.DeleteQueue`, which judges
  no grant (ADR-0146 decisions 3.4 and 3.7). Found by the code review of BL-1992 (2026-11-27), not run:
  - `DestroyKeptSubscription`: a link with no consumer (a no-source lookup, or one the container detached) is
    replaced by a more privileged client on another filter; its `closed` detach then deletes the new queue.
  - `EndSessionSubscriptions`: a name stays in `Amqp1Session.SessionSubscriptionQueues` after the same connection
    replaces the queue from another session, and that session's end deletes whatever holds the name.
  - `ClearStaleQueue`: the queue is inspected, judged and then deleted in separate bus operations, so a queue
    replaced in between is deleted unjudged.
- A bus operation that deletes only when the description matches (and, where asked, the queue is unused), under one
  hold of the lock, covers all three.

## Acceptance criteria

- [ ] `Surl.MessageBus.UnitTests`: the new operation deletes a queue carrying the given description and refuses, deleting nothing, one with another description or none, and one in use when asked only if unused.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: a `closed` detach of a lookup link, and a session end, leave a queue that has since been replaced by a subscription with another description.
- [ ] `ClearStaleQueue`, `DestroyKeptSubscription` and `EndSessionSubscriptions` use the operation, and both libraries' `CLAUDE.md` say so.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
