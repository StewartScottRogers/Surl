---
id: BL-373
title: Create Surl.Quic and its packet and frame codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-372]
touches: [Surl.slnx, Surl.Quic.UnitLibrary, Surl.Quic.UnitTests, Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed:
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
  `Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`.
- RFC 9000: section 16 (variable-length integers), 17 (packet formats: Initial, 0-RTT, Handshake,
  Retry, Version Negotiation, 1-RTT short header), 12.4 and 19 (frames), and 20 (error codes). This
  task covers headers and frames in the clear. Packet protection is BL-374.
- Fixtures: the decrypted `CRYPTO` data and datagrams BL-363 recorded, and RFC 9001 Appendix A's
  unprotected packets.

## Acceptance criteria

- [ ] Both projects exist and are listed in `Surl.slnx` in ordinal order. The production csproj has no
      `PackageReference`. `ProtocolIsolationTests` knows the row and passes.
- [ ] Round-trip tests cover each varint length (1, 2, 4 and 8 bytes) at its boundaries, each long
      header type, the short header, and each of the RFC 9000 section 19 frame types.
- [ ] RFC 9001 Appendix A's unprotected client Initial (A.2) parses into its header fields, and the
      `CRYPTO` and `PADDING` frames carry the payload offsets the appendix shows.
- [ ] Each malformed case is a named test with its error: a truncated varint, a frame type unknown in
      its packet type (`FRAME_ENCODING_ERROR`), a `STREAM` frame whose offset plus length exceeds
      2^62-1, an `ACK` range that underflows, and a connection ID longer than 20 bytes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
