---
id: BL-631
title: Hold named queues with competing consumers, acknowledgement and redelivery in Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-629]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-631 — Hold named queues with competing consumers, acknowledgement and redelivery in Surl.MessageBus

## Goal

`Surl.MessageBus` holds named queues bound to topics, each message delivered to one of the queue's consumers,
kept until acknowledged and redelivered when a consumer goes away or rejects it, bounded as BL-622's ADR says.

## Context

- Decision: BL-622's ADR, decision 3 (queues, competing consumers, acknowledgement, redelivery, durable flag,
  bounds). These are what AMQP 0-9-1 queues (`basic.consume`, `basic.ack`, `basic.reject`/`nack`), AMQP 1.0
  nodes with settlement, STOMP `/queue/…` with `client`/`client-individual` acks, and MQTT 5 shared
  subscriptions (`$share/`, MQTT 5.0 section 4.8.2) and NATS queue groups need; the ADR says which of them use
  bus queues and which keep their own behaviour.
- Library: BL-629's `Surl.MessageBus`. A queue binds to topic filters in the canonical space, so a message
  published on any protocol lands in every bound queue.
- No timers but the injected `TimeProvider`; no static state.
- Persistence of durable queues is BL-634's, not this task's.

## Acceptance criteria

- [x] Named tests show: a message published to a bound topic is queued; with two consumers each message goes to
      exactly one; an unacknowledged message is redelivered (marked as redelivered) when its consumer is
      disposed; a rejected message is redelivered or dropped as the ADR says; an acknowledged one is gone; a
      queue past its bound returns the ADR's typed outcome; a queue declared again with different flags returns
      the ADR's typed refusal.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Tests, by criterion: `BusQueueConsumerTests.Publish_OnABoundTopic_IsQueuedUntilAConsumerStarts`,
  `TwoConsumers_EachMessageGoesToExactlyOne_RoundRobin`,
  `ConsumerDisposed_UnacknowledgedMessageIsRedeliveredMarkedToAnotherConsumer`,
  `Reject_WithRequeue_IsRedeliveredMarked` and `Reject_WithoutRequeue_IsDropped`, `Acknowledge_MessageIsGone`;
  `MessageBusQueueBoundTests.PublishToQueue_PastMaxMessagesPerQueue_ReturnsQueueFull` and
  `MessageBusQueueDeclareTests.DeclareQueue_PastMaxQueues_ReturnsTooManyQueuesAndMakesNoQueue`;
  `MessageBusQueueDeclareTests.DeclareQueue_AgainWithDifferentFlags_ReturnsQueueFlagsDifferAndLeavesTheQueue`.
  All 295 tests in the project pass.
- Coverage on 2026-11-05: `Measure-CodeQuality.ps1 -Library Surl.MessageBus.UnitLibrary` reports line 100%,
  branch 100%, worst CRAP 16. It also reports three complexity failures in BL-630's name-mapping code, not in
  this task's code. BL-695 tracks them.
- Decisions, made under Stewart's delegation. ADR-0100 decisions 3.3 and 3.6 did not settle these:
  - Reject without requeue drops the message. 3.3 names only requeue as putting it back, and the bus has no
    dead-letter queue (AMQP 0-9-1 lets a server discard).
  - Redeclaring with other flags is `QueueFlagsDiffer` (AMQP 0-9-1 `queue.declare`: `406 precondition-failed`).
    Other connections reaching an exclusive queue get `QueueLocked` (`405`). An absent queue is
    `UnknownQueue` (`404`), an unknown delivery tag is `UnknownDeliveryTag` (`406`), and an empty name is
    `InvalidQueueName`. `QueueFull` covers both the per-queue message bound and the all-queues byte bound,
    because 3.6 gives both the same answer.
  - A consumer receives nothing until `BusQueueConsumer.Start`, so an AMQP server can send `basic.consume-ok`
    before the first `basic.deliver`.
  - Consumers count against `MaxSubscriptionsPerConnection`, the bound 3.6 gives for one connection's
    subscriptions.
  - Any connection may send to an exclusive queue (`PublishToQueue` takes no connection). Only the owner may
    declare, bind, consume from or delete it.
  - Delivery tags are unique per queue and new on each redelivery. `Redelivered` means the queue handed the
    message out before, even when the consumer ended before its callback ran.
  - `BusPublishResult` gains `QueuedCount` and `FullQueueCount` as init properties, so existing positional
    comparisons still hold.
- These decisions should go into an ADR. `Documentation/Planning/Decisions` is in BL-612's `touches` (Doing), so
  this task stayed inside its own `touches`. The choices are written here and in the library's `CLAUDE.md`,
  and BL-694 will amend ADR-0100 with them.
- Filed: BL-694 (ADR-0100 amendment) and BL-695 (pre-existing complexity failures).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.MessageBus holds named queues: bound to topics, competing consumers, acknowledgement, reject and redelivery marked in place, bounded with typed outcomes
