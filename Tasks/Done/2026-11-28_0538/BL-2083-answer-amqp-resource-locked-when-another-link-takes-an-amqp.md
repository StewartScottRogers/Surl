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
completed: 2026-11-28
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

- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: an unshared kept attach whose queue another link consumes between the inspect and the consume is answered `amqp:resource-locked`.
- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: the same for a queue declared in between with another description, and for one another connection declared exclusive.
- [x] `Surl.Extended.Protocol.Amqp1.UnitLibrary/CLAUDE.md` states the rule.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Plan: a change to one library's refusal mapping, so the `/feature` stages were done in the session, without
  agent delegation. No upstream curl measurement: `amqp1` is an extended scheme (ADR-0083). No new ADR: this
  carries out ADR-0146 decision 3.3.
- Tests first: the four new race cases (consume, declare with the same flags, declare with other flags, declare
  exclusive) failed with `amqp:not-found` before the change.
- `SubscriptionQueueRefusal` now maps `ExclusiveConsumeRefused`, `QueueDescriptionDiffers` and `QueueLocked`
  to `subscription in use`, beside `QueueInUse`. It also serves the delete in `ClearStaleQueue`, so a stale
  queue redeclared between the inspect and the delete is `amqp:resource-locked` too (tested).
- The bus answers `QueueFlagsDiffer` before it compares descriptions, so a queue declared in between with
  another description and other flags shows as that. `ConsumeRefusal` inspects the queue again: another
  description is `amqp:resource-locked`; this subscription's own description with other flags stays
  `amqp:not-found`, as the existing bounds test pins, since no other link took anything there.
- Result: build clean, 30842 fast tests passed, `Measure-CodeQuality.ps1 -Library` for the library: 0 failing
  members.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. An AMQP 1.0 attach that loses its subscription's queue name to another link or connection after the inspect is refused amqp:resource-locked
