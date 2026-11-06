---
id: BL-740
title: Refuse an HTTP/3 flow past a connection limit with CONNECTION_REFUSED
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-384]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-740 — Refuse an HTTP/3 flow past a connection limit with CONNECTION_REFUSED

## Goal

`Http3ProtocolServer` implements `IDatagramRefusalWriter` as ADR-0082 decision 7's first row says.

## Context

- ADR-0082 decision 7, "Too many connections": a flow past `--max-connections` or
  `--max-connections-per-address` whose first datagram is a version 1 Initial gets one Initial
  packet, protected with the Initial keys of the client's DCID, holding `CONNECTION_CLOSE` (0x1c)
  `CONNECTION_REFUSED` (0x02); any other first datagram gets no bytes.
- BL-384 composed `Http3ProtocolServer` (`Protocols/Http/Surl.Protocol.Http.UnitLibrary`)
  without it, so today such a flow is disposed with no bytes. `Surl.Quic` has
  `QuicInitialSecrets`, `QuicPacketProtection` and `QuicLongHeaderPacket` to build the packet.

## Acceptance criteria

- [ ] `Http3ProtocolServer` implements `IDatagramRefusalWriter`; a test in
      `Surl.Protocol.Http.UnitTests` feeds pinned upstream curl's recorded Initial
      (`Fixtures/h3-client-initial`) and decrypts the one datagram sent to a `CONNECTION_CLOSE`
      0x1c with error code 0x02.
- [ ] A first datagram that is not a version 1 Initial gets no bytes (a test pins it).
- [ ] `dotnet build -warnaserror` is clean, fast tests green, touched projects at 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
