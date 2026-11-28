---
id: BL-2071
title: Let a bus queue consumer take messages only as its delivery credit allows
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-2048]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2071 — Let a bus queue consumer take messages only as its delivery credit allows

## Goal

A named queue's consumer can bound by a delivery credit what the bus assigns to it, so a message it cannot take
stays ready or goes to another consumer.

## Context

- ADR-0146 Amendment 7 (BL-2048) decides this; item 2 is the bus part and items 5 and 7 say what callers see.
  BL-2072 then uses it in the AMQP 1.0 container.
- Operations: `BusQueueConsumer.SetDeliveryCredit(uint)` and `BusQueueConsumer.TryClaimDelivery` (answers `bool`
  and the delivery). A consumer never given a credit behaves exactly as before.
- Start at `BusQueueConsumer.cs` (`HasRoom`, `Take`, which sets `QueuedMessage.HandedOut`), `NamedQueue.cs`
  (`NextConsumerWithRoomFor`, `HasConsumerWithRoomFor`) and `MessageBus.Queues.cs` (`Dispatch`,
  `DeliverToConsumers`, which publishes `Delivered`, `Settle`).
- A held (assigned, unclaimed) delivery counts as unacknowledged. For a consumer with a credit, `HandedOut` and
  `Delivered` belong to the claim, not the assignment.
- Stand-in: no new option or fault; `Delivered` on the event stream means claimed.

## Acceptance criteria

- [ ] `Surl.MessageBus.UnitTests`: a started consumer given credit 0 is assigned nothing and another consumer of the queue gets every message (with none, they stay ready); one given credit n holds at most n; two with credit take turns; a consumer never given a credit is served as before, the existing tests unchanged.
- [ ] `Surl.MessageBus.UnitTests`: `TryClaimDelivery` answers held deliveries in the order assigned, lowers the credit by one each and publishes `Delivered` then and not at assignment; it answers `false` when none is held and after the consumer or queue has ended; `Acknowledge` and `Reject` of a held delivery answer `UnknownDeliveryTag` and change nothing.
- [ ] `Surl.MessageBus.UnitTests`: lowering the credit puts the deliveries assigned last back at their places with no event, and another consumer with room receives them; a held delivery returned that way, by the consumer's end or by `Recover`, saved to files or restored from a snapshot keeps the redelivered mark it had before assignment (unset for a message never claimed, set for one rejected with requeue).
- [ ] `Surl.MessageBus.UnitTests`: `PublishToQueue(..., onlyWhenConsumed: true)` answers `NoReadyConsumer` and `PublishToConsumedQueues` counts the queue unconsumed when its only consumer has no credit left.
- [ ] `Surl.MessageBus.UnitLibrary/CLAUDE.md`, the XML docs and a "delivery credit" row in `Documentation/Wiki/Glossary.md` state the rule.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-28: Created.
