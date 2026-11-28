---
id: BL-2026
title: Keep an AMQP 1.0 session end and replacement race from taking or misreporting a subscription another link holds
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-2026 — Keep an AMQP 1.0 session end and replacement race from taking or misreporting a subscription another link holds

## Goal

A session's end leaves a `session-end` subscription another link of the connection still consumes, and a stale
queue that gains a consumer before it can be replaced is answered `amqp:resource-locked`.

## Context

- Found by the code review of BL-2010 (2026-11-27), not run.
- `EndSessionSubscriptions` (`Amqp1ContainerConnection.Subscriptions.cs`) deletes each queue still carrying the
  session's description, in use or not. When session 1 makes a `session-end` subscription and detaches, and session 2
  resumes it with the same description (or shares it), session 1's end deletes the queue under session 2's link.
  `DeleteQueueIfDescribed(..., ifUnused: true)` is the likely answer.
- `ClearStaleQueue` answers `QueueInUse` (a consumer attached between its inspect and its delete) through
  `KeptRefusal` as `amqp:not-found`; ADR-0146 decision 3.3 says `amqp:resource-locked`. Only a race reaches it, so
  the change must keep 100% branch coverage.

## Acceptance criteria

- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: a session's end leaves a subscription another session's link still consumes, and that session's end deletes it.
- [ ] A queue in use when `ClearStaleQueue` deletes it is answered `amqp:resource-locked`.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitLibrary/CLAUDE.md` states both.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
