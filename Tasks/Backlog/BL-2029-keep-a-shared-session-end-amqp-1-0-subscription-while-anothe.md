---
id: BL-2029
title: Keep a shared session-end AMQP 1.0 subscription while another session still holds it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2010]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2029 — Keep a shared session-end AMQP 1.0 subscription while another session still holds it

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

- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: two sessions consume one shared `session-end` subscription; after the first ends, the second still receives what is then published; the test failed before the change, or BL-2010 had already fixed it and Notes say so.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: after one session's link detaches with `closed` false and the other session ends, the queue stays and keeps a message published then.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: after both sessions end, in either order, the queue is gone.
- [ ] The library's `CLAUDE.md` states the rule, and row `Amqp1:Core-2.6.6-2` cites the new tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
