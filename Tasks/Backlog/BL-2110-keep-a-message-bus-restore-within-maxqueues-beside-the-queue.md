---
id: BL-2110
title: Keep a message bus restore within MaxQueues beside the queues it keeps
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2110 — Keep a message bus restore within MaxQueues beside the queues it keeps

## Goal

A restore either never leaves the bus holding more than `MaxQueues` queues, or an ADR and the library's docs say when it may.

## Context

- Found while planning BL-2095 (2026-11-28), read in the code, not run.
- `ReplaceQueues` (`MessageBus.SeedResetSnapshot.cs`) keeps each live exclusive or consumed queue the snapshot does
  not hold, and `Refill` then adds every snapshot queue without checking `MaxQueues` (ADR-0006, ADR-0124).

## Acceptance criteria

- [ ] A `Surl.MessageBus.UnitTests` test fills the bus to `MaxQueues`, restores a snapshot of other queues while live queues are kept, and pins the decided behaviour.
- [ ] An ADR amendment and `Surl.MessageBus.UnitLibrary/CLAUDE.md` state the behaviour.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Renumbered from BL-2103 to BL-2110; an archived task holds BL-2103.
