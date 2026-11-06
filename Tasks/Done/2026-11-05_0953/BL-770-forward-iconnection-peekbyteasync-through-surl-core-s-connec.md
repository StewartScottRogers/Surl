---
id: BL-770
title: Forward IConnection.PeekByteAsync through Surl.Core's connection wrappers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-734]
touches: [Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-770 — Forward IConnection.PeekByteAsync through Surl.Core's connection wrappers

## Goal

A protocol server served by `Surl.Core`'s serving engine can call `IConnection.PeekByteAsync` on the connection it
is handed and get the transport's peek, instead of the default implementation's `NotSupportedException`.

## Context

- BL-734 added `IConnection.PeekByteAsync` (ADR-0113, decision 6) as a default interface member that throws
  `NotSupportedException`, so the many test doubles implementing `IConnection` kept compiling. `StreamConnection`
  (`Surl.Networking`) and `InMemoryConnection` implement it.
- The serving engine wraps every connection before handing it to a server: `RecordingConnection` and
  `IdleClockRestartingConnection` in `Foundation/Surl.Core.UnitLibrary` forward `ReadAsync` and the rest, but not the
  peek, so `natss` served from `surl` (BL-528) would throw.
- Decide per wrapper what the peek means: `RecordingConnection` should not log a peeked byte as received (the read
  that later consumes it logs it); whether a peek restarts the idle clock is `IdleClockRestartingConnection`'s call
  (a byte arriving is client activity, but the peek only observes it - record the choice under Notes).

## Acceptance criteria

- [x] `RecordingConnection.PeekByteAsync` and `IdleClockRestartingConnection.PeekByteAsync` forward to the wrapped
      connection, with named tests in `Surl.Core.UnitTests` showing a peeked byte returned and the later read still
      returning it, and a `null` peek passed through.
- [x] A serving-engine test over `InMemoryConnection` shows a server's peek through the engine's wrappers returns the
      script's next byte.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Core.UnitLibrary` keeps 100% line and
      branch coverage.

## Notes

- Filed by BL-734 (lane 9, 2026-11-05).
- Delivered directly: two one-line forwards with no new contract, so the full `/feature` pipeline was not needed.
- `RecordingConnection`: a peek is not logged; the read that consumes the byte logs it, so every received byte is
  logged exactly once.
- `IdleClockRestartingConnection` (decided by Claude under Stewart's delegation): a peek does not restart the idle
  clock. It only observes the byte; the read that consumes it restarts the clock, so a peek and its read count as one
  piece of client activity. No ADR: this applies ADR-0006 section 1 (the clock restarts when a read returns bytes)
  unchanged.
- Tests: `RecordingConnectionTests.PeekByteAsync_*` (2), `IdleClockRestartingConnectionTests.PeekByteAsync_*` (2),
  `ServingEngineTests.ServeAsync_ServerPeeksThroughTheEnginesWrappers_GetsTheScriptsNextByte`. Surl.Core.UnitTests
  138 passed; `Surl.Core.UnitLibrary` at 100% line and 100% branch.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Servers served by Surl.Core's engine can peek: RecordingConnection and IdleClockRestartingConnection forward PeekByteAsync
