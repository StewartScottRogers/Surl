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
completed: 2026-11-27
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

- [x] `Surl.MessageBus.UnitTests`: the new operation deletes a queue carrying the given description and refuses, deleting nothing, one with another description or none, and one in use when asked only if unused.
- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: a `closed` detach of a lookup link, and a session end, leave a queue that has since been replaced by a subscription with another description.
- [x] `ClearStaleQueue`, `DestroyKeptSubscription` and `EndSessionSubscriptions` use the operation, and both libraries' `CLAUDE.md` say so.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

- New bus operation `BusConnection.DeleteQueueIfDescribed(name, description, ifUnused)`; it shares one locked delete
  path with `DeleteQueue`. A `null` description matches only a queue with none, so `ClearStaleQueue` still replaces
  an undescribed queue under a subscription's name, as before, and never one described since it was inspected.
- A lookup link (no source) takes as its subscription the one found and judged at its attach, not the one planned from
  its address: a `closed` detach still deletes a persisted subscription whose address a changed `--topic-map` now
  maps elsewhere.
- `Amqp1Session.SessionSubscriptionQueues` became `SessionSubscriptions` (queue name to description).
- A session's end still deletes a queue in use that carries its description, as before; follow-up BL-2013. AMQP 0-9-1
  `queue.delete` still judges no grant; follow-up BL-2014.
- No ADR file written: these refine ADR-0146 decision 3.4 as this task states it, and
  `Documentation/Planning/Decisions` is in BL-1976's `touches` while this ran. The rules are in both libraries'
  `CLAUDE.md`.
- Verified 2026-11-27: `dotnet build -warnaserror` clean, `dotnet format --verify-no-changes` clean, all fast tests
  pass (MessageBus 607, Amqp1 866), `Measure-CodeQuality.ps1` gives both libraries 100% line and branch, 0 failing
  members. Code review found no defect; its documentation corrections are applied. No conformance run: an extended
  server, which upstream curl cannot speak (ADR-0083).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. AMQP 1.0 subscription queues are deleted only while they carry the description the link was judged on
