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
completed:
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

- [ ] Fast tests cover: a passive accept from the peer's address; a connection from another
      address refused and the listener still waiting until the timeout; the accept timeout; an
      active connect to the peer's address; an active request naming another address refused
      without connecting; a TLS upgrade on a data connection using the server's
      `ServerTlsSettings`.
- [ ] Integration tests open a real loopback passive and active data connection and move bytes
      both ways.
- [ ] `dotnet build Surl.Networking.UnitLibrary -warnaserror` is clean; the fast tests pass with
      no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary` reports
      100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
