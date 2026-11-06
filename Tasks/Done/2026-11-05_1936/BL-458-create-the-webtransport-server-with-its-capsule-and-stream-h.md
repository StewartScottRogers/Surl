---
id: BL-458
title: Create the WebTransport server with its capsule and stream-header codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-457, BL-404]
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Surl.slnx, Documentation/Conformance/Extended/WebTransport.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-458 — Create the WebTransport server with its capsule and stream-header codec

## Goal

`Surl.Extended.Protocol.WebTransport.UnitLibrary` exists. It encodes and decodes the WebTransport capsules and
the stream headers (`0x41` plus session ID, `0x54` plus session ID) with QUIC variable-length integers, and it
needs no HTTP/3 yet.

## Context

- Decision: BL-457's ADR, decisions 4 and 6. Rules: BL-543's ADR.
- New projects in `Protocols/Http/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.WebTransport`.
  References: `Surl.Protocol.Abstractions` only; BL-459 adds the HTTP/3 library.
- WTH3 (the draft revision the ADR pins): the capsule layouts and the stream headers. RFC 9297 section 3.2
  gives the capsule format (type, length, value). RFC 9000 section 16 gives variable-length integers. If
  `Surl.Quic` or the HTTP/3 library already exposes a public variable-length-integer codec, reuse it instead of
  copying it.

## Acceptance criteria

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - every capsule the ADR uses round-tripped, with bytes written from the draft's layouts;
      - a `WT_CLOSE_SESSION` with a 1024-byte message accepted and a 1025-byte one refused (WTH3 6, ADR-0117 decision 6);
      - each stream header read with a 1-, 2-, 4- and 8-byte session ID;
      - a truncated capsule reported as incomplete, not malformed.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- **Reference to `Surl.Quic`.** The Context says "Abstractions only" but also "reuse a public
  variable-length-integer codec if `Surl.Quic` exposes one". It does (`QuicVariableLengthInteger`), and
  `Surl.Quic` is a horizontal library in `ProtocolIsolationTests`' table, so the library references it rather
  than copying the codec. BL-459's `Surl.Http3` references it anyway. No ADR needed: ADR-0086's table allows it.
- **Shape.** One abstract `WebTransportCapsule` record with a record per layout: close-session, drain-session,
  datagram, a `WebTransportLimitCapsule` for the six one-integer flow-control capsules, and
  `WebTransportUnparsedCapsule` for unknown types and the HTTP/3-prohibited `WT_MAX_STREAM_DATA` (`0x190b4d3e`)
  and `WT_STREAM_DATA_BLOCKED` (`0x190b4d42`), taken from draft-ietf-webtrans-http2's codepoint sequence
  (ADR-0117 decision 4), so BL-460 can tell them by type. `WebTransportCapsuleReader.Read` reports
  `Incomplete` (bytes still to come), `Malformed` (whole but breaking its layout: close-session under 4 bytes,
  message over 1024 bytes or not UTF-8, drain-session with a value, a limit value that is not exactly one
  integer) or `Complete`. `TryReadHeader` exposes the declared length so BL-460 can apply `--max-message`
  before the value arrives. The close-session record also refuses a message over 1024 UTF-8 bytes when built.
- **Stream header.** `WebTransportStreamHeader.Read` calls a stream that starts with anything but `0x41`/`0x54`
  malformed; checking that the session ID is a client-initiated bidirectional stream ID (`H3_ID_ERROR`) is
  BL-459's, which owns that matrix row.
- **Error-code mapping** (WTH3 4.4, matrix row WTH3-4.4-2, which the matrix gave to BL-458):
  `WebTransportApplicationErrorCode`.
- **Matrix.** `Documentation/Conformance/Extended/WebTransport.md` is added to `touches`: its BL-458 rows are
  replaced by the named tests, as ADR-0117's Consequences ask. No task in Doing touches it.
- Tests: 87 in `Surl.Extended.Protocol.WebTransport.UnitTests`; the library measured 100% line and branch.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Extended.Protocol.WebTransport reads and writes every WebTransport capsule, both stream headers and the WT_APPLICATION_ERROR mapping, at 100% coverage
