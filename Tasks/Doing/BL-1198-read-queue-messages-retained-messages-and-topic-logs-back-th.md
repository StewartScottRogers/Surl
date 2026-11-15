---
id: BL-1198
title: Read queue messages, retained messages and topic logs back through ComposedStores
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1194, BL-1195]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-15
completed:
---
# BL-1198 — Read queue messages, retained messages and topic logs back through ComposedStores

## Goal

`ComposedStores` browses a queue's ready messages, reads a topic's retained message and reads a topic log's records without consuming anything, as BL-1194's ADR names them.

## Context

- Decision: the ADR filed by BL-1194. Primitives: BL-1195's non-consuming queue browse and retained lookup in `Surl.MessageBus`, and the existing `MessageBus.ReadLog` and `GetLogOffsets` (`Protocols/Messaging/Surl.MessageBus.UnitLibrary/MessageBus.Logs.cs`).
- Where: `Application/Surl.Composition.UnitLibrary/ComposedStores.cs`, beside `SeedPublishAsync` and `SeedEnqueueAsync`.
- Reads publish nothing, ask no fault rule, and work on a `--directory` bus.
- Stand-in fit: the code under test publishes over MQTT, AMQP, Kafka, NATS or STOMP, and the test checks what reached the queue, the retained slot or the log without a broker client of its own.

## Acceptance criteria

- [ ] `ComposedStores` has the bus read members the ADR names, documented with XML comments.
- [ ] A test in `Application/Surl.Composition.UnitTests` enqueues two messages with `SeedEnqueueAsync`, browses them back in order, and then shows the queue still holds both.
- [ ] A test publishes a retained message with `SeedPublishAsync` and reads it back. A test reads a topic log's records from offset 0.
- [ ] A test shows an unknown queue or topic gives the ADR's not-found result.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, the fast tests pass, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
