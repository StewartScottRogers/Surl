---
id: BL-1037
title: Test the AMQP 1.0 queue link's bound of 1000 unacknowledged deliveries
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-782]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-11
completed:
---
# BL-1037 — Test the AMQP 1.0 queue link's bound of 1000 unacknowledged deliveries

## Goal

A unit test in `Amqp1ContainerConnectionTests` shows that a queue link holds at most
`Amqp1ContainerConnection.MaxWaitingDeliveries` (1000) unacknowledged deliveries, and the
`Amqp1:Surl-Delivery-2` row of `Documentation/Conformance/Extended/Amqp1.md` names it.

## Context

- `Amqp1:Surl-Delivery-2` (ADR-0107 decision 7): "A queue link consumes with at most 1000
  unacknowledged deliveries; those not sent when it ends go back to the queue."
- The bound is passed to the bus in `Amqp1ContainerConnection.Links.cs`
  (`busConnection.Consume(queueName, ..., MaxWaitingDeliveries, ...)`), and
  `Amqp1ContainerConnection.Deliveries.cs` stops sending at 1000 unsettled. BL-782 found no queue
  test of it: `ServeAsync_ThousandUnsettledTopicDeliveries_HoldTheNextUntilOneIsSettled` covers a
  topic link only.
- Pattern to copy: `ServeAsync_ThousandUnsettledTopicDeliveries_HoldTheNextUntilOneIsSettled` in
  `Amqp1ContainerConnectionTests.Settlement.cs`, with 1001 messages enqueued on a queue
  (`Enqueue(bus, "orders", ...)`) and a receiver given credit 2000.

## Acceptance criteria

- [ ] `Amqp1ContainerConnectionTests.ServeAsync_ThousandUnsettledQueueDeliveries_HoldTheNextUntilOneIsSettled`
      exists and passes: 1001 enqueued, 1000 transfers written while none is settled, 1001 after one
      `accepted` disposition.
- [ ] The `Amqp1:Surl-Delivery-2` row names that test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-11: Created.
