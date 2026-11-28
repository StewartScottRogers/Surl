---
id: BL-2083
title: Answer amqp:resource-locked when another link takes an AMQP 1.0 subscription between its inspect and its consume
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-2026]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2083 — Answer amqp:resource-locked when another link takes an AMQP 1.0 subscription between its inspect and its consume

## Goal

An unshared kept subscription that another link takes after the container inspected its queue is refused with
`amqp:resource-locked`, not `amqp:not-found`.

## Context

- Found while working BL-2026 (2026-11-28) by reading the code; not run, so prove it with a failing test first.
- `Amqp1ContainerConnection.Subscriptions.cs`: `MakeQueueAndConsume` passes the outcome of
  `DeclareBindAndConsume` to `SubscriptionQueueRefusal`, which maps `ExclusiveConsumeRefused` (another link
  consumes first), `QueueDescriptionDiffers` (another description was declared first) and `QueueLocked` (another
  connection declared it exclusive first) to `amqp:not-found`. ADR-0146 decision 3.3 says a subscription another
  link holds is `amqp:resource-locked`.
- `ServeAsync_StaleSubscriptionThatGainsAConsumerBeforeItIsReplaced_...` shows how a test reaches such a race: a
  `TimeProvider` that acts when the bus reads its clock during the access judgement.

## Acceptance criteria

- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: an unshared kept attach whose queue another link consumes between the inspect and the consume is answered `amqp:resource-locked`.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: the same for a queue declared in between with another description, and for one another connection declared exclusive.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitLibrary/CLAUDE.md` states the rule.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
