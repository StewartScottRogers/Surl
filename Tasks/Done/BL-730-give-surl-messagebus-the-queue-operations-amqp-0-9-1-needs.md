---
id: BL-730
title: Give Surl.MessageBus the queue operations AMQP 0-9-1 needs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-706]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-730 — Give Surl.MessageBus the queue operations AMQP 0-9-1 needs

## Goal

`Surl.MessageBus` can inspect a queue without creating it, purge it, hand out one message at a time, delete it on
condition, hold exclusive consumers, publish only to consumed queues, stop a consumer while keeping its deliveries,
and deliver two priority levels, so the AMQP 0-9-1 broker (BL-506) can answer ADR-0112's decisions 5 and 6.

## Context

- Decision: ADR-0112 decisions 5 and 6 (and its amendment of ADR-0100 decision 3.3). Rules: ADR-0100 decision 3
  (the bus, its outcomes and its bounds). Today `BusConnection` has `DeclareQueue` (creates when absent),
  `BindQueue`, `UnbindQueue`, `DeleteQueue` (outcome only), `Consume`, and `BusQueueConsumer` has `Acknowledge`,
  `Reject`, `Recover` and `Dispose` (which requeues its unacknowledged deliveries).
- AMQP 0-9-1 XML: `queue.declare` rule `passive` (404 without creating), `queue.purge` (count), `queue.delete`
  rules `in-use` and `not-empty` (406) and `delete-ok`'s message count, `basic.get` (`get-ok` with the remaining
  count, `get-empty`), `basic.consume` rule `01` (`access-refused`: exclusive access to a queue with consumers),
  `basic.publish`'s `immediate`, `basic.cancel` (delivered messages stay outstanding), basic class rules 06 and 08
  (priorities 0-4 and 5-9 as two levels, each in order).
- Planning names (refine in the ADR's spirit): `InspectQueue` (outcome, ready count, consumer count; refused
  `UnknownQueue`, `QueueLocked`), `PurgeQueue` (count), `GetFromQueue` (one `BusQueueDelivery` held for the
  connection until acknowledged, rejected or the connection ends), `DeleteQueue(ifUnused, ifEmpty)` returning the
  count and a new outcome for the refused condition, `Consume(..., exclusive)` with a new outcome for an exclusive
  refusal, a publish that copies only into queues with a ready consumer and reports how many took it,
  `BusQueueConsumer.Stop` (no more deliveries, outstanding ones kept), and a high-priority mark on a queued message.
- Every new outcome gets its AMQP 0-9-1 answer in ADR-0112's decision 7 table; persistence (ADR-0100 decision 3.7)
  keeps each message's priority.

## Acceptance criteria

- [x] Named tests show: a queue inspected without being created; a purge removing ready messages only and counting
      them; a single get held unacknowledged and requeued when its connection ends; a delete refused under
      `if-unused` and `if-empty` and otherwise counting the messages; an exclusive consumer refusing a second
      consumer and a second consumer refusing an exclusive one; a publish to consumed queues only; a stopped
      consumer keeping its outstanding deliveries until settled; high-priority messages delivered before normal
      ones, each level in order, also after a save and load.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-503 (ADR-0112). Depends on BL-706 because both change `Surl.MessageBus` and must not run at once.
- Delivered: `InspectQueue`, `PurgeQueue`, `GetFromQueue` with `AcknowledgeGet`/`RejectGet`,
  `DeleteQueue(name, ifUnused, ifEmpty)`, `Consume(..., exclusive)`, `PublishToConsumedQueues`,
  `PublishToQueue(..., onlyWhenConsumed)`, `BusQueueConsumer.Stop` and `BusMessage.HighPriority`. The new outcomes
  are `QueueInUse`, `QueueNotEmpty`, `ExclusiveConsumeRefused` and `NoReadyConsumer`. The 22 tests are in
  `MessageBusQueueOperationsTests`.
- Choices (defaults taken):
  - A get is held by a hidden per-connection get holder in the queue's consumer list. It is never started, and never
    counted as a consumer or against `MaxSubscriptionsPerConnection`. It is settled by queue name and delivery tag,
    since tags are unique within a queue.
  - `if-empty` counts every message, ready or unacknowledged, so a delete never drops a message a consumer holds
    while calling the queue empty. `delete-ok`'s count is every message deleted.
  - A stopped consumer stays in the queue until disposed, so its outstanding deliveries keep counting and persisting.
    It no longer counts as a consumer for inspection, `if-unused` or exclusivity. An auto-delete queue goes when its
    last consumer that is not a get holder is disposed.
  - "A consumer able to take it now" means a started, unstopped consumer with room under `maxUnacknowledged`.
  - The priority is a `BusMessage` property, so a topic publish carries it into bound queues. It persists as bit
    `0x02` of `queues/index`'s per-message byte, beside redelivered `0x01`, because the codec's flags byte was full.
    Files written before this load unchanged. Topic logs do not keep the level, because ADR-0112 keeps fan-out and
    logs in publish order.
- Added `Documentation/Conformance/Extended/Amqp0.md` to touches, to replace its BL-730 trace with the named tests.
  No task in Doing names it.
- ADR-0112 decision 7 and ADR-0100 still need the outcome names. BL-524 in Doing touches
  `Documentation/Planning/Decisions`, so that went to BL-734 rather than widening this task.
- `Measure-CodeQuality.ps1 -Library Surl.MessageBus.UnitLibrary` reports 100% line and 100% branch coverage. Its four
  remaining Cobertura complexity flags are on members this task did not touch, and the build-time CA1502 gate is clean.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.MessageBus inspects, purges, gets one message, deletes on condition, holds exclusive consumers, publishes to consumed queues only, stops consumers keeping deliveries, and delivers two priority levels, persisted
