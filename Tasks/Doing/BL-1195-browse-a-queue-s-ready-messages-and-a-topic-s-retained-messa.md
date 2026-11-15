---
id: BL-1195
title: Browse a queue's ready messages and a topic's retained message without consuming them in Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1194]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: FR-061
created: 2026-11-15
completed:
---
# BL-1195 — Browse a queue's ready messages and a topic's retained message without consuming them in Surl.MessageBus

## Goal

`MessageBus` returns a queue's ready messages and a topic's retained message without consuming, acknowledging or reordering anything, within the bounds BL-1194's ADR sets.

## Context

- The ADR filed by BL-1194 decides the read-back of the message bus. This task builds the two primitives the bus lacks, so `ComposedStores` can expose them (BL-1198).
- Today `BusConnection.InspectQueue` (`Protocols/Messaging/Surl.MessageBus.UnitLibrary/BusConnection.cs`) returns only `ReadyMessageCount` and `ConsumerCount` (`BusQueueInspectResult`), and `GetFromQueue` consumes. A retained message is reached only through `Subscribe(..., includeRetained: true)`. Topic logs already have `ReadLog` and `GetLogOffsets` (`MessageBus.Logs.cs`), so they need nothing here.
- Lock and order as the bus's other reads do (`MessageBus.Queues.cs`, `MessageBus.SeedResetSnapshot.cs`). A browse takes the bus's lock, copies, and releases. It publishes no event and asks no fault rule.
- Stand-in fit: reads leave the store exactly as it was, so a test can browse then let the code under test consume. They work on a `--directory` bus too.

## Acceptance criteria

- [ ] `MessageBus` has a browse of a named queue's ready messages, oldest first, up to a count the caller gives. It has a lookup of a topic's retained message, `null` when there is none. Both are named and bounded as the ADR says.
- [ ] Tests in `Protocols/Messaging/Surl.MessageBus.UnitTests` show that a browse returns the messages in order and leaves `InspectQueue`'s `ReadyMessageCount` and the next `GetFromQueue` result unchanged.
- [ ] A test shows a browse of an unknown queue returns the bus's not-found outcome, and a retained lookup on a topic with no retained message returns none.
- [ ] A test shows neither read publishes a `StoreChangedEvent` (recording publisher from `Surl.Protocol.Abstractions`' test doubles).
- [ ] `dotnet build Protocols/Messaging/Surl.MessageBus.UnitLibrary -warnaserror` is clean, the fast tests pass, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
