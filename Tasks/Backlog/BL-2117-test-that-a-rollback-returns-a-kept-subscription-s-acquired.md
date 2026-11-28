---
id: BL-2117
title: Test that a rollback returns a kept subscription's acquired AMQP 1.0 delivery to its queue
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2117 — Test that a rollback returns a kept subscription's acquired AMQP 1.0 delivery to its queue

## Goal

A unit test pins that rolling back a transaction returns a delivery acquired from a kept subscription's queue to that
queue, marked redelivered.

## Context

- Found while working BL-2113 (2026-11-28). The `Amqp1:Transactions-4.4.3-1` row of
  `Documentation/Conformance/Extended/Amqp1.md` says this of a kept or non-durable shared subscription's deliveries.
- The rollback tests in `Amqp1ContainerConnectionTests.TransactionalRetirement.cs` cover a queue node's delivery and a
  live topic subscription's only. `RollBack` in `Amqp1ContainerConnection.Transactions.cs` takes the same path for a
  subscription queue's delivery, because it has a queue delivery tag.

## Acceptance criteria

- [ ] A test in `Surl.Extended.Protocol.Amqp1.UnitTests` acquires a kept subscription's delivery in a transaction, rolls it back, and asserts the message is ready in the subscription's queue and marked redelivered.
- [ ] The `Amqp1:Transactions-4.4.3-1` row names that test under "Covered by".

## Notes

## Log

- 2026-11-28: Created.
