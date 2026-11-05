---
id: BL-830
title: Stop the NATS outbox calling a late write after Complete a slow consumer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-830 — Stop the NATS outbox calling a late write after Complete a slow consumer

## Goal

`NatsOutbox.Enqueue` after `Complete` drops the bytes without setting `HasOverflowed`, so a connection that is ending is
never answered `-ERR 'Slow Consumer'` unless it really passed the pending-bytes bound.

## Context

- `NatsOutbox.Enqueue` falls through to `Interlocked.Exchange(ref overflowed, 1)` whenever `TryWrite` fails, which it
  also does once the channel is completed, so a bus delivery racing the connection's end marks the outbox overflowed and
  `NatsServerConnection.ServeAsync` then writes the slow-consumer error (ADR-0113 decision 7).
- BL-532 found and fixed the same flaw in its copy, `StompOutbox.Enqueue`: overflow only when the pending bytes pass the
  bound. Follow that fix.

## Acceptance criteria

- [ ] A test named `NatsOutboxTests.Enqueue_AfterComplete_IsDroppedWithoutOverflowing` (or the class's existing
      equivalent) passes: `Complete`, then `Enqueue`, leaves `HasOverflowed` false.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-05: Created.
