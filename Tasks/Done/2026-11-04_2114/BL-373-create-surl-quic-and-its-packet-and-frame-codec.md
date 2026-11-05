---
id: BL-373
title: Create Surl.Quic and its packet and frame codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-372]
touches: [Surl.slnx, Surl.Quic.UnitLibrary, Surl.Quic.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-373 — Create Surl.Quic and its packet and frame codec

## Goal

`Surl.Quic.UnitLibrary` and `Surl.Quic.UnitTests` exist and are listed in `Surl.slnx`. The library reads
and writes QUIC variable-length integers, packet headers (long, short, Version Negotiation, Retry) and
every frame type of RFC 9000 as typed values, and rejects malformed input with the transport error RFC
9000 names.

## Context

- Decision: BL-372's ADR, decisions 2 and 4. `Surl.Quic` is the planning name. If the ADR names it
  differently, use the ADR's name everywhere. If the ADR decides on `System.Net.Quic` on every
  platform with no hand-built QUIC, move this task to `Deferred` with the ADR as the reason.
- To create the project, copy BL-292's change for `Surl.HttpMessage` (commit `5008a375`): the csproj
  pair, `CLAUDE.md`, two `Surl.slnx` lines in ordinal order, and the row in
  `Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`.
- RFC 9000: section 16 (variable-length integers), 17 (packet formats: Initial, 0-RTT, Handshake,
  Retry, Version Negotiation, 1-RTT short header), 12.4 and 19 (frames), and 20 (error codes). This
  task covers headers and frames in the clear. Packet protection is BL-374.
- Fixtures: the decrypted `CRYPTO` data and datagrams BL-363 recorded, and RFC 9001 Appendix A's
  unprotected packets.

## Acceptance criteria

- [x] Both projects exist and are listed in `Surl.slnx` in ordinal order. The production csproj has no
      `PackageReference`. `ProtocolIsolationTests` knows the row and passes.
- [x] Round-trip tests cover each varint length (1, 2, 4 and 8 bytes) at its boundaries, each long
      header type, the short header, and each of the RFC 9000 section 19 frame types.
- [x] RFC 9001 Appendix A's unprotected client Initial (A.2) parses into its header fields, and the
      `CRYPTO` and `PADDING` frames carry the payload offsets the appendix shows.
- [x] Each malformed case is a named test with its error: a truncated varint, a frame type unknown in
      its packet type (`FRAME_ENCODING_ERROR`), a `STREAM` frame whose offset plus length exceeds
      2^62-1, an `ACK` range that underflows, and a connection ID longer than 20 bytes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line
      and branch coverage.

## Notes

- ADR-0082 keeps the name `Surl.Quic` and puts it in `Protocols/Http/` (decision 2); QUIC is
  hand-built (decision 1), so the task goes ahead. The `ProtocolIsolationTests` row is
  `[Quic] = [Tls13, ChaCha20, Poly1305]`, as decision 2 lists it. The codec references none of them yet.
  In `Surl.slnx` the pair sits after `Surl.Protocol.Ws.*` and before `Surl.WebSocket.*`, because
  ordinal order puts `P` before `Q`.
- Errors, chosen from RFC 9000 where it allows more than one (sensible defaults):
  - An unknown frame type is `FRAME_ENCODING_ERROR` (section 12.4).
  - A known frame type sent in a packet type that table 3 does not allow is `PROTOCOL_VIOLATION`,
    as section 12.4 says. The criterion's "frame type unknown in its packet type" covers both, and
    each has its own named test.
  - A `STREAM` frame ending past 2^62-1 is `FRAME_ENCODING_ERROR`. Section 19.8 allows that or
    `FLOW_CONTROL_ERROR`.
  - A `CRYPTO` frame ending past 2^62-1 is `CRYPTO_BUFFER_EXCEEDED` (section 19.6 allows that or
    `PROTOCOL_VIOLATION`).
  - A frame type that is not in its shortest encoding is refused as `PROTOCOL_VIOLATION` (12.4 "MAY").
  - A packet-header fault is `PROTOCOL_VIOLATION` with no frame type. The caller drops the packet,
    as sections 5.2 and 17.2 require.
- Header connection IDs follow RFC 8999. A long header with a version other than 0 or 1 reads as
  `QuicUnsupportedVersionPacket`, whose connection IDs may be up to 255 bytes. This lets the server
  answer it with Version Negotiation (ADR-0082 decision 4). Only version 1 refuses a connection ID
  over 20 bytes.
- The packet reader works on unprotected packets (reserved bits must be 0). For a protected
  packet, BL-374 must first find the packet number offset and remove header protection.
- Fixtures: RFC 9000 Appendix A.1's varints and RFC 9001 Appendix A.2 to A.5: the client Initial,
  server Initial, Retry and ChaCha20 short header. The CRYPTO frame text in A.2 matches the
  self-test in `Run-QuicInitialRecorder.cs`. BL-363 committed no decrypted datagrams of its own,
  only ClientHellos in `Surl.Tls13.UnitTests`, so the appendix is the fixture here.
- Measured: `Surl.Quic.UnitLibrary` has 100% line and 100% branch coverage and no failing member
  (`Measure-CodeQuality.ps1 -SkipTestRun` over the project's cobertura run). It has 144 tests.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Quic reads and writes QUIC varints, every packet header and every RFC 9000 frame, refusing malformed input with RFC 9000's transport errors
