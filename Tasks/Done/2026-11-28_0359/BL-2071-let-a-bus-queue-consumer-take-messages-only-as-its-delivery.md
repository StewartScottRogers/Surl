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
completed: 2026-11-28
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

- [x] `Surl.MessageBus.UnitTests`: a started consumer given credit 0 is assigned nothing and another consumer of the queue gets every message (with none, they stay ready); one given credit n holds at most n; two with credit take turns; a consumer never given a credit is served as before, the existing tests unchanged.
- [x] `Surl.MessageBus.UnitTests`: `TryClaimDelivery` answers held deliveries in the order assigned, lowers the credit by one each and publishes `Delivered` then and not at assignment; it answers `false` when none is held and after the consumer or queue has ended; `Acknowledge` and `Reject` of a held delivery answer `UnknownDeliveryTag` and change nothing.
- [x] `Surl.MessageBus.UnitTests`: lowering the credit puts the deliveries assigned last back at their places with no event, and another consumer with room receives them; a held delivery returned that way, by the consumer's end or by `Recover`, saved to files or restored from a snapshot keeps the redelivered mark it had before assignment (unset for a message never claimed, set for one rejected with requeue).
- [x] `Surl.MessageBus.UnitTests`: `PublishToQueue(..., onlyWhenConsumed: true)` answers `NoReadyConsumer` and `PublishToConsumedQueues` counts the queue unconsumed when its only consumer has no credit left.
- [x] `Surl.MessageBus.UnitLibrary/CLAUDE.md`, the XML docs and a "delivery credit" row in `Documentation/Wiki/Glossary.md` state the rule.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Plan: ADR-0146 Amendment 7 item 2 as written. A consumer with a credit keeps assigned deliveries in a
  `held` list beside `unacknowledged`; a claim moves one across, so settling sees only claimed ones.
- Choices the amendment leaves open, each following from "a held delivery counts as unacknowledged" or
  from the existing consumer rules, documented in the XML docs and pinned by tests where observable:
  a held message does not expire (an expired one returned to ready is dropped when the queue reaches
  it); a stopped consumer keeps what it holds, claimable until it ends; `SetDeliveryCredit` after the
  end does nothing; the claim's `Delivered` names the consumer connection's exchange.
- New tests are in `BusQueueConsumerDeliveryCreditTests.cs`; no existing test changed.
- Nothing measured against upstream curl: no wire bytes change.
- Verified 2026-11-28: `dotnet build -warnaserror` 0 warnings, `dotnet format` clean, fast tests 30707
  passed, 0 failed; `Surl.MessageBus.UnitLibrary` 100% line and branch, 0 failing members.
- Reviewed by `code-reviewer`: no behaviour defect; its documentation corrections and reset and expiry
  tests are in.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. A bus queue consumer given a delivery credit holds at most that many unclaimed deliveries and takes each only when it claims it
