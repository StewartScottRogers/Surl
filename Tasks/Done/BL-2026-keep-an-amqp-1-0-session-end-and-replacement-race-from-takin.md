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
completed: 2026-11-28
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

- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: a session's end leaves a subscription another session's link still consumes, and that session's end deletes it.
- [x] A queue in use when `ClearStaleQueue` deletes it is answered `amqp:resource-locked`.
- [x] `Surl.Extended.Protocol.Amqp1.UnitLibrary/CLAUDE.md` states both.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- The session-end defect was already fixed by BL-2043 (done 2026-11-28, after this task was filed): a session holds
  every `session-end` subscription it consumed, and the queue goes with its last holder.
  `ServeAsync_SessionEndWhileAnotherSessionsLinkConsumesTheSessionEndSubscription_LeavesItUntilThatSessionEnds`
  (unshared and shared) passed without a library change.
- **Decided by Claude under Stewart's delegation:** no `ifUnused: true` on `EndSessionSubscriptions`' delete. Every
  link consuming a `session-end` queue is in a session that holds it, and the queue is exclusive to the connection,
  so the condition could never refuse and no test could tell it apart. No ADR: no behaviour changes.
- The race: `ServeAsync_StaleSubscriptionThatGainsAConsumerBeforeItIsReplaced_IsRefusedResourceLockedAndStays`
  failed first with `amqp:not-found`. It adds the consumer from a `TimeProvider` that acts when the bus reads its
  clock during the access judgement, between the inspect and the delete, so no bus change was needed. The fix is one
  arm in `SubscriptionQueueRefusal` (`QueueInUse` is `subscription in use`); only that delete can answer it.
- No plan, review or conformance agent: one switch arm in an extended server, which upstream curl cannot speak.
- The sibling race at the declare and consume step is filed as BL-2077.
- 2026-11-28: build 0 warnings, format clean, fast tests 30694 passed, 0 failed, 3 skipped; the library 100% line
  and branch coverage, 0 failing members.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. An AMQP 1.0 stale subscription that gains a consumer before replacement is refused amqp:resource-locked; a session's end leaves a session-end subscription another session consumes (BL-2043), now tested
