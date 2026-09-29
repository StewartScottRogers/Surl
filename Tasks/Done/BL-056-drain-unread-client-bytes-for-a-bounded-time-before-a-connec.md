---
id: BL-056
title: Drain unread client bytes for a bounded time before a connection closes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-011, BL-024]
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests, Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-056 — Drain unread client bytes for a bounded time before a connection closes

## Goal

Closing a connection whose client sent bytes the server never read (a refused request
body, a pipelined request) no longer resets it, so upstream curl receives the whole
response instead of failing with exit code 56 ("Recv failure: Connection reset by peer").

## Context

- `StreamConnection.DisposeAsync` (BL-011) sends FIN and then disposes the socket at once.
  Linux and Windows answer unread bytes in the receive buffer at close with RST, and a
  client that has not yet read the tail of the response can lose it.
- The usual answer is a lingering close: after FIN, read and discard what the client
  sends until it half-closes or a bounded time passes on the injected `TimeProvider`,
  then close. The bound is a per-exchange limit, which BL-024 decides.
- Measure the failure first: a loopback exchange with a pinned upstream curl build
  (`Record-CurlExchange.ps1`) that sends a body the server does not read, before and after.
- Stays inside the `IConnection` contract (ADR-0004, section 2): disposal still closes
  gracefully and never throws.

## Acceptance criteria

- [x] A fast test shows `DisposeAsync` reads and discards inbound bytes after FIN until
      the peer half-closes, then disposes the stream.
- [x] A fast test on a fake `TimeProvider` shows the drain stops at the bound BL-024 sets.
- [x] An `[TestCategory("Integration")]` test on `127.0.0.1:0`: the client sends bytes the
      server never reads, the server writes a reply and disposes, and the client reads the
      whole reply without a reset.
- [x] `Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary` reports no failing
      member; `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Filed by BL-011 (code review, 2026-09-28).
- Measured first (2026-09-29, pinned curl 8.21.0 win-x64): a 413 with a 200000-byte body
  sent after the head of a 4 MiB upload, then an immediate close with the body unread,
  made curl exit 56 (`Recv failure: Connection was reset`) in 2 of 3 runs, with 65-102 KB
  received; with a lingering close curl exited 0 with all 200000 bytes, 3 of 3. To measure
  it, `Record-CurlExchange.ps1` gained `-CloseUnread` (with `-RespondAfterBodyBytes`).
  One run of each is kept in `Surl.Networking.UnitTests/Fixtures/lingering-close-*`.
- BL-024 (ADR-0006) set no drain bound, so ADR-0021 decides it, under Stewart's
  delegation: 2 seconds (`StreamConnection.LingeringCloseTime`) on the injected
  `TimeProvider`, no byte limit (one 4096-byte discard buffer), read below TLS, skipped
  after `Abort` or a failed half-close. A fixed constant rather than an `ExchangeLimits`
  member, because it ends an exchange rather than limiting one, and
  `Surl.Protocol.Abstractions` is outside this task.
- `touches` widened: `Documentation/Planning/Decisions` (ADR-0021 and the index) and
  `Record-CurlExchange.ps1` (`-CloseUnread`); no task in `Doing` (BL-053, BL-054) names
  either.
- The integration test fails 3 of 3 with a connection reset when the drain is disabled,
  and passes with it. Networking: 245 tests (224 fast, 21 Integration) green; the whole
  solution's fast tests and the Console and Conformance Integration tests green;
  `Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary`: 0 failing members.
- BL-061 (HTTP drain of refused bodies) still stands: it reads unread request bytes at the
  HTTP layer so the next request on a kept connection is framed correctly; this task's
  drain is only the transport's close.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Disposing a TCP connection lingers up to 2 s after FIN, discarding unread client bytes, so upstream curl reads the whole reply instead of exit 56 (ADR-0021)
