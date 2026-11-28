---
id: BL-2043
title: Keep a shared session-end AMQP 1.0 subscription while another session still holds it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2010]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2043 — Keep a shared session-end AMQP 1.0 subscription while another session still holds it

## Goal

A shared `session-end` subscription consumed from two sessions of a connection lasts until both sessions have
ended.

## Context

- Found while working BL-1990 (2026-11-27) by reading the code; a possible defect, so prove it with a failing test
  first.
- `Amqp1ContainerConnection.Subscriptions.cs`: `MakeQueueAndConsume` adds the queue name to each consuming
  session's `Amqp1Session.SessionSubscriptionQueues`, and `EndSessionSubscriptions` deletes every name in the
  ending session's set. Ending one session therefore deletes the queue under the other session's consumer.
- BL-2010 changes the same method to a conditional delete. It may already keep a queue that is in use; check what
  is left. A session whose link detached with `closed` false still holds the subscription (ADR-0146 decision 3.6:
  kept until its session ends) although no consumer takes from the queue.
- A `closed` detach keeps today's rule (ADR-0146 decision 3.5: destroyed with the last link attached).
- Matrix row `Amqp1:Core-2.6.6-2` in `Documentation/Conformance/Extended/Amqp1.md`.
- Stand-in: the shared message bus. No new event or fault.

## Acceptance criteria

- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: two sessions consume one shared `session-end` subscription; after the first ends, the second still receives what is then published; the test failed before the change, or BL-2010 had already fixed it and Notes say so.
- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: after one session's link detaches with `closed` false and the other session ends, the queue stays and keeps a message published then.
- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: after both sessions end, in either order, the queue is gone.
- [x] The library's `CLAUDE.md` states the rule, and row `Amqp1:Core-2.6.6-2` cites the new tests.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- The defect was real after BL-2010: `ServeAsync_OneOfTwoSessionsHoldingASharedSessionEndSubscriptionEnds_...`
  and `ServeAsync_SessionEndWhileADetachedLinksSessionStillHoldsTheSharedSubscription_...` both failed before
  the change. BL-2010's delete was conditional on the description only, so it still deleted a queue in use.
- **Decided by Claude under Stewart's delegation:** a `session-end` subscription goes with the last session of
  the connection that consumed it. The rule covers an unshared one resumed in a second session too: it had the
  same defect (either session's end deleted it, under the other's consumer if there was one), and one rule for
  both is the simplest. A session holds it whether or not its link still consumes it (ADR-0146 decision 3.6).
- When a `closed` detach or a replacement deletes the queue, every session's record of it is dropped, so a
  session that held the destroyed one cannot keep a later subscription of the same name alive.
- No other connection can hold one: a `session-end` queue is exclusive to its connection.
- The ADR note is filed as BL-2062, because BL-2044 (in Doing) was editing ADR-0146.
- No plan or conformance agent: a two-method fix in an extended server, which upstream curl cannot speak.
  `code-reviewer` found no correctness bug; its wording findings are fixed and it prompted the unshared test.
- 2026-11-28: build 0 warnings, format clean, fast tests 30661 passed, 0 failed, 3 skipped; the library 100%
  line and branch coverage, 0 failing members.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. A session-end AMQP 1.0 subscription held by several sessions of a connection now lasts until the last of them ends
