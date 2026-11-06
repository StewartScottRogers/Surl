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
completed: 2026-11-06
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

- [x] `Http3ProtocolServer` implements `IDatagramRefusalWriter`; a test in
      `Surl.Protocol.Http.UnitTests` feeds pinned upstream curl's recorded Initial
      (`Fixtures/h3-client-initial`) and decrypts the one datagram sent to a `CONNECTION_CLOSE`
      0x1c with error code 0x02.
- [x] A first datagram that is not a version 1 Initial gets no bytes (a test pins it).
- [x] `dotnet build -warnaserror` is clean, fast tests green, touched projects at 100% line and branch coverage.

## Notes

- Plan: the packet is built in `Surl.Quic` as `QuicConnectionRefusal.Write(firstDatagram, random)` (no I/O, testable against all three pinned curl Initials in `Fixtures/ClientInitial`); `Http3ProtocolServer.WriteRefusalAsync` sends what it returns on the flow, the same for both `ConnectionRefusal` values.
- Choice: "a version 1 Initial" is read as exactly what `QuicServerConnection` would start a connection on - a datagram of at least 1200 bytes, long header, version 1, fixed bit set, type Initial, Destination Connection ID of at least 8 bytes - and whose packet opens with the clients Initial keys. Anything else (including a forged header that does not decrypt) gets no bytes, so the refusal never answers a datagram the server would have dropped and cannot be used to reflect bytes at a spoofed address for garbage. Within ADR-0082 decision 7 as written, so no new ADR.
- Choice: the refusal Initial goes to the clients Source Connection ID from a random 8-byte server ID (`ServerConnectionIdLength`), token empty, packet number 0 (1 byte), payload only `CONNECTION_CLOSE` 0x1c, error 0x02, frame type 0, no reason phrase (`1c 02 00 00`). It is not ack-eliciting, so it is not padded to 1200 bytes (RFC 9000 section 14.1 pads only ack-eliciting Initials); 21 bytes of header protection sample are guaranteed by the 4-byte payload plus the 16-byte tag.
- Learned: a protected long header cannot go through `QuicPacketReader.Read` before it is opened - the reserved bits are under header protection and read as set about three times in four - so the header is read by hand only as far as the Destination Connection ID, as `QuicServerConnection.StartFromInitial` does.
- Tests: `QuicConnectionRefusalTests` (Surl.Quic.UnitTests, 12) and four new `Http3ProtocolServerTests.WriteRefusalAsync_*` (Surl.Protocol.Http.UnitTests). Coverage: Surl.Quic.UnitLibrary and Surl.Protocol.Http.UnitLibrary each 100% line and branch from their own tests. Fast run: 79 test projects green.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Http3ProtocolServer refuses a flow past a connection limit with one Initial holding CONNECTION_CLOSE CONNECTION_REFUSED, and sends nothing for any other first datagram
