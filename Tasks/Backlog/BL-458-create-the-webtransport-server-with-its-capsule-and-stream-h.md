---
id: BL-458
title: Create the WebTransport server with its capsule and stream-header codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-457, BL-404]
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
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

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - every capsule the ADR uses round-tripped, with bytes written from the draft's layouts;
      - a `WT_CLOSE_SESSION` with a 1024-byte message accepted and a 1025-byte one refused (WTH3 6, ADR-0117 decision 6);
      - each stream header read with a 1-, 2-, 4- and 8-byte session ID;
      - a truncated capsule reported as incomplete, not malformed.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
