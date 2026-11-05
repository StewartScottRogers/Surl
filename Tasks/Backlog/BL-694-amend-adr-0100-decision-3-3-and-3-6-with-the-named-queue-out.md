---
id: BL-694
title: Amend ADR-0100 decision 3.3 and 3.6 with the named-queue outcomes and rules BL-631 chose
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-631]
touches: [Documentation/Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-694 — Amend ADR-0100 decision 3.3 and 3.6 with the named-queue outcomes and rules BL-631 chose

## Goal

ADR-0100 decisions 3.3 and 3.6 state the named-queue rules and typed outcomes that BL-631 built into
`Surl.MessageBus`, so that the Amqp0, Amqp1 and Stomp servers can map each outcome to their protocol's reply.

## Context

- BL-631 could not edit `Documentation/Planning/Decisions` because BL-612 held it at the time. It recorded these
  choices in its own Notes (Tasks, BL-631) and in `Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md`.
  Read both.
- New `MessageBusOutcome` members: `InvalidQueueName`, `UnknownQueue` (AMQP 0-9-1 `404 not-found`), `QueueLocked`
  (`405 resource-locked`), `QueueFlagsDiffer` (`406 precondition-failed`) and `UnknownDeliveryTag` (`406`).
  `TooManyQueues` and `QueueFull` are already in the decision 3.6 table. `QueueFull` also covers `MaxQueuedBytes`.
- Rules: a reject without requeue drops the message (no dead-lettering); a consumer receives nothing until
  `BusQueueConsumer.Start`, so that consume-ok goes first; consumers count against
  `MaxSubscriptionsPerConnection`; any connection may send to an exclusive queue, but only its owner may bind,
  consume or delete it; `BusPublishResult.QueuedCount` and `FullQueueCount` report the copies a topic publish
  made into bound queues.

## Acceptance criteria

- [ ] The ADR-0100 decision 3.6 outcome table has rows for `UnknownQueue`, `QueueLocked`, `QueueFlagsDiffer`,
      `UnknownDeliveryTag` and `InvalidQueueName`, with the answer each of Amqp0, Amqp1 and Stomp gives.
- [ ] ADR-0100 decision 3.3 states the reject-without-requeue rule, the `Start` ordering rule and the
      exclusive-queue access rule, each marked "Decided by Claude under Stewart's delegation".

## Notes

## Log

- 2026-11-05: Created.
