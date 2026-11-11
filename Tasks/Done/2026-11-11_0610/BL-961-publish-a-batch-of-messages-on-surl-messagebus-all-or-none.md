---
id: BL-961
title: Publish a batch of messages on Surl.MessageBus all or none
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-961 — Publish a batch of messages on Surl.MessageBus all or none

## Goal

`BusConnection` publishes an ordered batch of messages - each either to a topic or straight to a named queue -
all or none: when any one would be refused, none is published, so an AMQP 1.0 transaction commits atomically.

## Context

- ADR-0146 decision 1.6 (a `discharge` with `fail` false publishes the transaction's held messages all or
  none, else `amqp:transaction:rollback`) and its "Alternatives considered" (why not in order, as AMQP 0-9-1's
  `tx` does in ADR-0112 decision 6).
- Today `BusConnection.Publish` and `MessageBus.PublishToQueue` publish one message at a time; the bounds that
  refuse one are ADR-0100 decision 3.6's (`StoreFull`, `TooManyTopics`, `QueueFull` on a direct queue publish,
  `MessageTooLarge`, `AccessDenied`, `UnknownQueue`, `InvalidTopic`). A bound queue that is full when a topic
  publish copies into it does not refuse the publish (`BusPublishResult.FullQueueCount`); that stays so in a batch.
- Each published message raises the same events and store changes as a single publish (ADR-0124), in batch order.
- `Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md` lists what the bus holds; add the batch to it.

## Acceptance criteria

- [x] `BusConnection` has a batch publish taking an ordered list of entries (topic message, or queue name and
      message) and returning the outcome and, when refused, the index of the first entry refused and why.
- [x] A test shows a batch whose third entry would be `StoreFull` (or `QueueFull` to a direct queue) publishes
      nothing: no subscriber, queue, topic log or retained slot sees any entry, and no store change is raised.
- [x] A test shows a batch that fits publishes every entry in order, with subscribers, queues and events seeing
      them in that order, exactly as the same single publishes would.
- [x] The check and the publish happen under one hold of the bus's lock, so no other publish interleaves.
- [x] `Surl.MessageBus.UnitLibrary/CLAUDE.md` describes the batch; 100% line and branch coverage holds.

## Notes

- Filed by BL-707 (ADR-0146 decision 1.6).
- Delivered directly in the lane (one library and its tests), not through the full `/feature`
  sub-agent pipeline; the design is small and ADR-0146 already decides the behaviour.
- Design: `BusConnection.PublishBatch(IReadOnlyList<BusBatchEntry>)` -> `BusBatchPublishResult(Outcome,
  RefusedIndex, Results)` (`MessageBus.Batch.cs`). Lock-free checks (topic, size, access, fault plan)
  run first in order, stopping at the first refusal; then one `ChangeState` hold plans every entry
  with a counting `BatchPlan` (no mutation, no events) and, only when all fit, publishes each through
  the single publish's own `Accept`/`Enqueue`. Chosen over apply-then-roll-back because a rollback would
  have to undo dispatches to consumers, round-robin positions, versions and events.
- The plan is exact for logs (a `PlannedLog` replays `TopicLog.OldestToDrop` over held and planned
  records) and retained bytes. For queues it counts upward only; dispatch can only drop expired
  messages, so the real queue never holds more than planned. The bound queues the plan found full are
  passed to `CopyIntoBoundQueues`, so a copy the plan refused is refused for real even if expiry freed
  room - otherwise a later direct entry could fail after the plan said it fits.
- Choice: `Delivered` events cannot interleave with the other changes as separate single publishes
  would, because deliveries run outside the lock (ADR-0124). The test pins that non-delivery changes
  and deliveries each keep the single publishes' order. Subscribers and queues see the entries in
  batch order.
- Choice: the fault plan is asked outside the lock (as for a single publish), so it may be asked about
  an entry after one the locked check refuses; a `FaultAppliedEvent` may then be raised for a batch
  answered with an earlier refusal. No store change is raised either way.
- Tests: `BusConnectionPublishBatchTests` (15). `Surl.MessageBus.UnitLibrary` 100% line and branch.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. BusConnection.PublishBatch publishes an ordered batch of topic and queue entries all or none under one hold of the bus's lock
