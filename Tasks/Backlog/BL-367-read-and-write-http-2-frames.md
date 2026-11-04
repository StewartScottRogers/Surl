---
id: BL-367
title: Read and write HTTP/2 frames
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-364]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-367 — Read and write HTTP/2 frames

## Goal

The HTTP server reads and writes HTTP/2 frames (RFC 9113 sections 4 and 6) as typed values: the 9-byte
frame header and the ten frame types, with padding, flags and the maximum frame size. Every malformed
frame maps to the connection or stream error RFC 9113 names.

## Context

- Decision: BL-364's ADR, decision 4, places the frame codec. The planning assumption is
  `Surl.Protocol.Http.UnitLibrary`. If the ADR places it in another library, do the work there.
- RFC 9113 section 4.1 (frame format), 4.2 (frame size, `SETTINGS_MAX_FRAME_SIZE`) and section 6
  (`DATA`, `HEADERS`, `PRIORITY`, `RST_STREAM`, `SETTINGS`, `PUSH_PROMISE`, `PING`, `GOAWAY`,
  `WINDOW_UPDATE`, `CONTINUATION`). Unknown frame types are ignored (section 5.5). Error codes are in
  section 7.
- Reading is bounded, as every reader in the solution is. A frame longer than the advertised maximum
  is a `FRAME_SIZE_ERROR` and is never buffered.
- The bytes BL-364 measured from pinned upstream curl (its preface, `SETTINGS`, `WINDOW_UPDATE` and
  `HEADERS`) are the inbound test fixtures.

## Acceptance criteria

- [ ] Tests parse BL-364's recorded upstream curl frames into the expected typed values, and write
      each typed frame the server sends back to the bytes RFC 9113 defines.
- [ ] Each malformed case is a named test with the RFC 9113 error it maps to: a frame over the
      maximum size, `SETTINGS` with a length that is not a multiple of 6, a `SETTINGS` ACK with a
      payload, `PING` that is not 8 bytes, `WINDOW_UPDATE` with increment 0, padding longer than the
      payload, `DATA` or `HEADERS` on stream 0, and `SETTINGS` or `PING` on a non-zero stream.
- [ ] An unknown frame type is read and skipped.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
