---
id: BL-734
title: Let a protocol server peek at a connection's next byte before a TLS upgrade
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-524]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-734 — Let a protocol server peek at a connection's next byte before a TLS upgrade

## Goal

`IConnection` gains a peek: a protocol server can wait up to a given time for the client's next byte and learn it
(or learn that none came) without consuming it, so a later `ReadAsync` or `UpgradeToTlsAsync` still sees that byte.

## Context

- Decision: ADR-0113 decision 6 (BL-524). On `natss` the NATS server waits 50 ms for the client's first byte: a
  TLS-first client (`handshake_first`) has sent a ClientHello, so the server runs the handshake before INFO;
  otherwise it sends INFO with `tls_required` and then peeks again: `0x16` (a TLS handshake record) means the
  client is starting TLS and the server calls `UpgradeToTlsAsync`; any other byte is a plain-text CONNECT, answered
  `-ERR 'Secure Connection - TLS Required'`. Both need the byte left unread.
- Planning shape (the implementer may refine it): `ValueTask<byte?> PeekByteAsync(TimeSpan wait, CancellationToken)`
  on `IConnection`, `null` when nothing arrived within `wait` or the peer half-closed. `InMemoryConnection` gets
  the same, so protocol tests stay off the network (ADR-0004). `Surl.Networking`'s socket connection keeps the
  peeked bytes for the next read and hands them to the TLS handshake ahead of the socket's (a prefix stream under
  the `SslStream`); waiting runs on the injected `TimeProvider`.
- Only `Surl.Networking.UnitLibrary` constructs sockets and `SslStream` (root `CLAUDE.md`).

## Acceptance criteria

- [x] Named tests show, on `InMemoryConnection` and on the socket connection: a peek returning the next byte and the
      following `ReadAsync` still returning it; a peek returning `null` after the wait with nothing sent; a peek
      followed by `UpgradeToTlsAsync` completing a handshake whose first byte was peeked.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

- Filed by BL-524 (lane 2, 2026-11-05): ADR-0113 decision 6 needs it for BL-527.
- Shape (decided by Claude under Stewart's delegation, within ADR-0113 decision 6, so no new ADR):
  `ValueTask<byte?> PeekByteAsync(TimeSpan wait, CancellationToken)` on `IConnection`, a negative `wait` refused.
  It is a **default interface member that throws `NotSupportedException`**: about 40 test doubles and wrappers across
  the solution implement `IConnection`, almost all outside this task's `touches`, and none of them needs a peek.
  The cost is that `Surl.Core`'s wrappers (`RecordingConnection`, `IdleClockRestartingConnection`) do not forward it
  yet; filed as BL-764 and added to BL-528's `depends-on` (BL-527 tests on `InMemoryConnection` and is not affected).
- `StreamConnection`: a peek starts one one-byte read that is **never cancelled** and waits for it with
  `Task.WaitAsync(wait, timeProvider, token)`. Cancelling a socket read to end the wait could lose a byte that arrives
  at that moment; keeping the read means the next peek or read sees it. `ReadAsync` returns the peeked byte alone
  (one byte is a legal read). `UpgradeToTlsAsync` refuses while that read is still waiting (a pending read), and
  otherwise hands `ServerTlsHandshake` a `PeekedBytePrefixStream` (the byte, then the transport). `DisposeAsync`
  waits for that read, within the lingering time, before its discard loop, so two reads never race on the transport.
- `InMemoryConnection` gained an optional trailing `peekClock` (`TimeProvider`) for the wait past the end of a script
  whose client stays open; every existing caller compiles unchanged.
- Tests: `StreamConnectionPeekTests` (14, over `InMemoryDuplexStream`, a real `SslStream` handshake for the upgrade),
  `PeekedBytePrefixStreamTests` (8), `InMemoryConnectionTests.PeekByteAsync_*` (8), `PeekContractTests` (1).
  `Measure-CodeQuality.ps1` on both libraries: 100% line, 100% branch, worst CRAP 10. `ThrowIfCannotUpgrade` measured
  complexity 14 in Cobertura after the change, so the pending check moved into `IsReadOrWritePending`.
- A first run flaked one test under full-solution load with a 50 ms real-clock wait; the real-clock tests now wait up
  to 30 s (a peek returns as soon as its byte arrives) and the timing-out tests run on `ManualTimeProvider`.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. IConnection.PeekByteAsync returns the client's next byte without consuming it, on InMemoryConnection and StreamConnection, and a later read or TLS handshake still sees it
