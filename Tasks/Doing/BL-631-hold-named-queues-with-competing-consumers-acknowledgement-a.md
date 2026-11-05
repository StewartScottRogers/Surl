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
completed:
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

- [ ] Named tests show: a message published to a bound topic is queued; with two consumers each message goes to
      exactly one; an unacknowledged message is redelivered (marked as redelivered) when its consumer is
      disposed; a rejected message is redelivered or dropped as the ADR says; an acknowledged one is gone; a
      queue past its bound returns the ADR's typed outcome; a queue declared again with different flags returns
      the ADR's typed refusal.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
