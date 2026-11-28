---
id: BL-2062
title: Limit the AMQP 1.0 library's topic delivery comments to live subscriptions
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1ContainerConnection.Deliveries.cs, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1OutgoingDelivery.cs, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1OutgoingTransfer.cs, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1PendingMessage.cs]
requirement: none
created: 2026-11-28
completed:
---
# BL-2062 — Limit the AMQP 1.0 library's topic delivery comments to live subscriptions

## Goal

The AMQP 1.0 library's comments say that only a live topic subscription's delivery has no queue delivery tag and
nothing to put back.

## Context

- Found while working BL-2044 (2026-11-28). All files are in
  `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary`.
- `Amqp1ContainerConnection.Deliveries.cs`, the comment above `ApplyOutcome`: "A topic delivery has nothing to put
  back". That holds for a live subscription only. A topic link on a subscription's bus queue, a kept subscription's or
  a non-durable shared one's (ADR-0146 decision 3 and Amendment 4), is sent queue deliveries
  (`Amqp1ContainerConnection.Subscriptions.cs`, `OfferSubscriptionQueueDelivery` passes `delivery.DeliveryTag`), so
  `ApplyOutcome` acknowledges, drops or puts them back on that queue.
- The same drift is in the XML comments "`null` for a topic's" on `QueueDeliveryTag`: the parameter in
  `Amqp1OutgoingDelivery.cs` and in `Amqp1PendingMessage.cs`, and the parameter and property in
  `Amqp1OutgoingTransfer.cs`. Only `Amqp1ContainerConnection.Links.cs`, `OfferTopicMessage` (a live subscription),
  passes a null tag.
- BL-2045 corrects similar statements in other files of this library (`CLAUDE.md`, `Amqp1DeliveryState.cs`,
  `Amqp1ContainerConnection.Links.cs`); do not edit those here.
- Comments only; no behaviour, store, event or fault changes.

## Acceptance criteria

- [ ] The comment above `ApplyOutcome` says only a live topic subscription's delivery has nothing to put back, and that outcomes act on the queue for a delivery from a kept or non-durable shared subscription's queue.
- [ ] Each of the four `QueueDeliveryTag` comments named in Context says the tag is null only for a live topic subscription's delivery.
- [ ] The diff changes only comment lines in the four files named in `touches`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
