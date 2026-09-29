---
id: BL-069
title: Dispose the transport after a failed graceful close in Surl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-069 — Dispose the transport after a failed graceful close in Surl.Networking

## Goal

Disposing a `StreamConnection` always disposes its transport stream (and so its socket),
and never waits without bound for a graceful close, even after a cancelled write.

## Context

- Found by the code review of BL-025. Since BL-025 the serving engine cancels writes in
  flight (an idle timeout on a peer that stopped reading, the one-second refusal
  deadline) and then disposes the connection without aborting it (ADR-0006 section 5:
  every limit hit is a graceful close).
- `Surl.Networking.UnitLibrary/StreamConnection.cs`, `DisposeAsync` and
  `TryCompleteWritesAsync`: the graceful close calls `CompleteWritesAsync` with
  `CancellationToken.None` and catches only `IOException`. After a cancelled `SslStream`
  write, `ShutdownAsync` can throw `InvalidOperationException` or `NotSupportedException`,
  and `transportStream.DisposeAsync()` is then never reached, so the socket leaks. On TLS,
  a `close_notify` to a peer that stopped reading can also block forever, holding the
  engine's connection-limit slot (released only after dispose) and stalling shutdown.
- ADR-0006 section 5 gives a one-second write deadline to a refusal; use the same bound
  for the graceful close, on the connection's `TimeProvider` if it has one, and record the
  choice under Notes.

## Acceptance criteria

- [ ] A fast test proves the transport stream is disposed when the graceful close throws
      an exception other than `IOException`.
- [ ] A fast test proves `DisposeAsync` completes within the chosen bound when the graceful
      close never completes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `Surl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
