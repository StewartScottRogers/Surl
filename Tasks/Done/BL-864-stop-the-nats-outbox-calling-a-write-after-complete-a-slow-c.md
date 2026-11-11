---
id: BL-864
title: Stop the NATS outbox calling a late write after Complete a slow consumer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-864 — Stop the NATS outbox calling a late write after Complete a slow consumer

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

- [x] A test named `NatsOutboxTests.Enqueue_AfterComplete_IsDroppedWithoutOverflowing` (or the class's existing
      equivalent) passes: `Complete`, then `Enqueue`, leaves `HasOverflowed` false.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- The fix follows BL-532's `StompOutbox.Enqueue`. The outbox now overflows only when the pending bytes pass the
  bound. A write refused because the channel is complete is dropped quietly. The overflow path still completes the
  channel and cancels the writer. No test class existed, so `NatsOutboxTests` is new:
  `Enqueue_AfterComplete_IsDroppedWithoutOverflowing` (it fails before the fix), `Enqueue_PastTheBound_Overflows`
  and `Enqueue_PastTheBoundTwice_StaysOverflowed`.
- Verification toolchain (choice): the pinned SDK 10.0.401 is not installed on the machine (BL-1035). The library and
  its tests were therefore built (warnings as errors) and run with SDK 11.0.100, invoked from a folder outside the
  repository so `global.json` did not apply. All 184 fast tests pass. Cobertura shows the library at 100% line and
  100% branch coverage. `dotnet format` could not load MSBuild that way, so it was not run. CI builds with the pinned
  SDK before the shift merges to `master`, so a difference there stops the merge.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. NatsOutbox.Enqueue after Complete drops the bytes without overflowing, so only a real slow consumer gets -ERR 'Slow Consumer'
