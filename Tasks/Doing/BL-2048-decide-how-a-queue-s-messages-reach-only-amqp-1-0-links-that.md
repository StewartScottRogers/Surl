---
id: BL-2048
title: Decide how a queue's messages reach only AMQP 1.0 links that have credit
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2040]
touches: [Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2048 — Decide how a queue's messages reach only AMQP 1.0 links that have credit

## Goal

ADR-0146 states whether a message of a queue or subscription may wait on a link with no credit while another link
consuming the same queue has some, and if not, how the container and the bus prevent it.

## Context

- Found while working BL-2040 (2026-11-27) by reading the code; nothing was run.
- `Amqp1ContainerConnection.Links.cs` (`Consume`) and `Amqp1ContainerConnection.Subscriptions.cs`
  (`MakeQueueAndConsume`) consume with 1000 unacknowledged, and the bus chooses among consumers by that room alone
  (`Surl.MessageBus.UnitLibrary`, `NamedQueue.NextConsumerWithRoomFor`). So a link that has issued no credit still
  takes its round-robin share, and those messages wait on it until it flows or ends. Prove it with a test before
  deciding.
- Links that compete: `/queue/` links, shared kept subscriptions (ADR-0146 decision 3.5) and non-durable shared
  ones (Amendment 4, BL-2041).
- A test stand-in is expected to behave as a broker does: a JMS consumer that is not receiving should not hold
  messages another consumer is waiting for. Part 2 section 2.6.7 (link flow control) is the specification to read.
- If the answer needs a `Surl.MessageBus` change, file the build tasks, bus first. Amend only by adding a dated
  amendment.

## Acceptance criteria

- [ ] ADR-0146 has a dated amendment, marked "Decided by Claude under Stewart's delegation", saying what a queue's messages do when one consuming link has credit and another has none, with the alternative rejected and why.
- [ ] Any build task the amendment needs is filed, bus before container, with its `depends-on` set.
- [ ] The diff to ADR-0146 only adds lines.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
