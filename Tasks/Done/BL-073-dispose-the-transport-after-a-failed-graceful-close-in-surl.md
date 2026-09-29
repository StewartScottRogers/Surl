---
id: BL-073
title: Dispose the transport after a failed graceful close in Surl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-073 â€” Dispose the transport after a failed graceful close in Surl.Networking

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

- [x] A fast test proves the transport stream is disposed when the graceful close throws
      an exception other than `IOException`.
- [x] A fast test proves `DisposeAsync` completes within the chosen bound when the graceful
      close never completes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `Surl.Networking.UnitLibrary`.

## Notes

- Bound: `StreamConnection.GracefulCloseTime` = 1 second, the same bound ADR-0006
  section 5 gives a refusal's write, timed on the connection's `TimeProvider`
  (`TimeProvider.System` when none is given). `SslStream.ShutdownAsync` takes no
  token, so the whole graceful close is awaited with `WaitAsync(token)` as well as
  passing the token to the flush.
- Every exception from the graceful close is now swallowed, not only `IOException`:
  the close is best-effort and the transport must be disposed whatever went wrong
  (`InvalidOperationException`/`NotSupportedException` after a cancelled `SslStream`
  write, a timeout, a reset). A timed-out close leaves `writesCompleted` false, so the
  lingering close is skipped and the transport is disposed at once.
- Test helper: `ManualTimeProvider.TimersCreated(count)` added, because disposal now
  creates the graceful-close timer before the lingering one.
- Tests: `DisposeAsync_GracefulCloseThrowsOtherThanIOException_StillDisposesTheStream`,
  `DisposeAsync_GracefulCloseNeverCompletes_DisposesTheStreamAtTheGracefulCloseTime`.
  Surl.Networking.UnitTests 269/269 (248 fast); Measure-CodeQuality: 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Disposing a StreamConnection always disposes its transport: the graceful close is bounded at one second and any failure in it is swallowed
