---
id: BL-175
title: Open passive and active FTP data connections in Surl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-174]
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: FR-036
created: 2026-09-29
completed: 2026-09-29
---
# BL-175 — Open passive and active FTP data connections in Surl.Networking

## Goal

`Surl.Networking` implements BL-174's data-connection contract over sockets: a passive listener
on the control connection's local address that accepts one connection within the timeout and
only from the control connection's peer, an active connect only to the peer's own address, and
TLS on a data connection, so `Surl.Console` (BL-182) can compose real FTP data connections.

## Context

- Decisions: BL-173's ADR (address rules, port range, timeouts, TLS on data connections, the
  refusals and how each is reported to the server); ADR-0010 (server-side TLS with
  `ServerTlsSettings`); ADR-0006 section 4 (TLS versions); ADR-0021 (a lingering close for
  every TCP connection); ADR-0022 (an accept failure of one client does not stop anything else).
- Code: `Surl.Networking.UnitLibrary/TcpConnectionListener.cs`, `StreamConnection.cs`,
  `ServerTlsHandshake.cs`, `SocketTransportControl.cs`, `ListenerBinder.cs` - reuse them; this is
  the one library allowed to construct `Socket` and `SslStream`.
- Tests: logic (peer-address checks, timeouts on a fake `TimeProvider`, refusals) is covered by
  fast tests through the library's existing internal seams; tests that open a loopback socket
  are `[TestCategory("Integration")]` (NFR-001), as the existing listener tests are.

## Acceptance criteria

- [x] Fast tests cover: a passive accept from the peer's address; a connection from another
      address refused and the listener still waiting until the timeout; the accept timeout; an
      active connect to the peer's address; an active request naming another address refused
      without connecting; a TLS upgrade on a data connection using the server's
      `ServerTlsSettings`.
- [x] Integration tests open a real loopback passive and active data connection and move bytes
      both ways.
- [x] `dotnet build Surl.Networking.UnitLibrary -warnaserror` is clean; the fast tests pass with
      no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary` reports
      100% line and branch coverage and no failing member.

## Notes

- Delivered as ADR-0052 decision 9 names it: `SocketDataConnectionOpener` (public), with the
  rules in `SocketPassiveDataListener` and `DataConnectionPeer`, fast-tested over
  `FakeDataConnectionSockets`; only `SocketDataConnectionSockets` touches a socket (excluded
  from coverage, exercised by `SocketDataConnectionOpenerIntegrationTests` on `127.0.0.1` and
  `[::1]`). Measured: 100% line, 100% branch, 150 members, 0 failing, worst CRAP 10.
- Choices ADR-0052 left open, taken as defaults (no new ADR: each is inside the contract's
  documented failures):
  - A listening socket that fails (not one client's accept failure) is `Unreachable`, so the
    FTP server answers `425` as for every other data-connection failure.
  - The passive listener closes its listening socket as soon as it hands its connection out,
    so nothing else can connect while the transfer runs; a second `AcceptAsync` is
    `InvalidOperationException` (the contract hands out one connection).
  - A connection from another address is reset (`Abort`-style), not closed gracefully, so the
    2-second lingering close never holds up the wait for the peer.
  - Passive and active timeouts run on the injected `TimeProvider`; the caller's own
    cancellation stays `OperationCanceledException`, only the timeout is `TimedOut`.
  - The active socket is not bound to the control connection's local address: the contract
    passes no local end point and curl does not check the data connection's source address.
  - Data-connection TLS offers no ALPN (`TlsApplicationProtocols.ForScheme("ftp")`), as the
    control connection does.
- The passive listener's tests sit in `SocketDataConnectionOpenerTests`, because every one
  starts the listener through the opener as the FTP server will.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.Networking opens passive and active FTP data connections over sockets, peer-checked, timed out and TLS-capable (SocketDataConnectionOpener)
