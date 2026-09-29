---
id: BL-047
title: Drain unread client bytes for a bounded time before a connection closes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-011, BL-024]
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-047 — Drain unread client bytes for a bounded time before a connection closes

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

- [ ] A fast test shows `DisposeAsync` reads and discards inbound bytes after FIN until
      the peer half-closes, then disposes the stream.
- [ ] A fast test on a fake `TimeProvider` shows the drain stops at the bound BL-024 sets.
- [ ] An `[TestCategory("Integration")]` test on `127.0.0.1:0`: the client sends bytes the
      server never reads, the server writes a reply and disposes, and the client reads the
      whole reply without a reset.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary` reports no failing
      member; `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Filed by BL-011 (code review, 2026-09-28).

## Log

- 2026-09-28: Created.
