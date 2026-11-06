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
completed:
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

- [ ] `BusConnection` has a batch publish taking an ordered list of entries (topic message, or queue name and
      message) and returning the outcome and, when refused, the index of the first entry refused and why.
- [ ] A test shows a batch whose third entry would be `StoreFull` (or `QueueFull` to a direct queue) publishes
      nothing: no subscriber, queue, topic log or retained slot sees any entry, and no store change is raised.
- [ ] A test shows a batch that fits publishes every entry in order, with subscribers, queues and events seeing
      them in that order, exactly as the same single publishes would.
- [ ] The check and the publish happen under one hold of the bus's lock, so no other publish interleaves.
- [ ] `Surl.MessageBus.UnitLibrary/CLAUDE.md` describes the batch; 100% line and branch coverage holds.

## Notes

- Filed by BL-707 (ADR-0146 decision 1.6).

## Log

- 2026-11-06: Created.
