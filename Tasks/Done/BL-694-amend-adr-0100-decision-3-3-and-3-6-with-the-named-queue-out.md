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
completed: 2026-11-06
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

- [x] The ADR-0100 decision 3.6 outcome table has rows for `UnknownQueue`, `QueueLocked`, `QueueFlagsDiffer`,
      `UnknownDeliveryTag` and `InvalidQueueName`, with the answer each of Amqp0, Amqp1 and Stomp gives.
- [x] ADR-0100 decision 3.3 states the reject-without-requeue rule, the `Start` ordering rule and the
      exclusive-queue access rule, each marked "Decided by Claude under Stewart's delegation".

## Notes

- Made the edit in the session instead of delegating to `align-and-document`, because it is one ADR file and
  the content was already decided in BL-631's Notes.
- ADR-0100 now has: an "Amended" line in the header; "The named queue's rules" after decision 3.3's table
  (reject without requeue drops, `Start` ordering, consumers count against `MaxSubscriptionsPerConnection`,
  exclusive-queue access, the four refusal outcomes, delivery tags, `QueuedCount`/`FullQueueCount`), with
  the three rules marked "Decided by Claude under Stewart's delegation"; and "The queue outcomes" table
  after decision 3.6's table, plus a note that `QueueFull` covers `MaxQueuedBytes`.
- Each answer in the 3.6 table was taken from the servers as they are now, not invented:
  `Amqp0BrokerConnection.Exchanges.cs` `ChannelRefusals` and ADR-0112; `Amqp1ContainerConnection.Links.cs`
  `OpenNode` and `Amqp1ContainerConnection.Transfers.cs` `StoreRejection` and ADR-0107;
  `StompServerConnection.Routing.cs` `DeclareQueue` and `RefusalMessage` and ADR-0118. STOMP answers
  `QueueLocked` and `UnknownQueue` with `Resource limit exceeded` (its default refusal); the ADR records
  that as it is. "Does not arise" marks outcomes a server never lets the bus return to it.
- `Surl.MessageBus.UnitLibrary/CLAUDE.md` still says ADR-0100 "does not name yet" these outcomes, but
  BL-704 (Doing) holds that file, so BL-915 was filed to correct it.
- No `.cs` or project file changed, so the build and tests were not affected; they were run anyway.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0100 decisions 3.3 and 3.6 record BL-631's named-queue rules and the five queue outcomes with each queue server's answer
