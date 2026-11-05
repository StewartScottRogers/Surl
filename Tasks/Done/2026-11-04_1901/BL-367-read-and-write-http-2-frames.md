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
completed: 2026-11-04
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

- [x] Tests parse BL-364's recorded upstream curl frames into the expected typed values, and write
      each typed frame the server sends back to the bytes RFC 9113 defines.
- [x] Each malformed case is a named test with the RFC 9113 error it maps to: a frame over the
      maximum size, `SETTINGS` with a length that is not a multiple of 6, a `SETTINGS` ACK with a
      payload, `PING` that is not 8 bytes, `WINDOW_UPDATE` with increment 0, padding longer than the
      payload, `DATA` or `HEADERS` on stream 0, and `SETTINGS` or `PING` on a non-zero stream.
- [x] An unknown frame type is read and skipped.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

- Placement: ADR-0081 decision 4 puts frames in `Surl.Protocol.Http.UnitLibrary`, the planning
  assumption, so no `touches` change.
- BL-364 committed no fixture files (its bytes are in ADR-0081's tables), so the inbound fixture
  was recorded here: `Fixtures/h2-prior-knowledge-get`, pinned static 8.21.0 with
  `Record-CurlExchange.ps1 -Raw`, 165 bytes byte-for-byte as ADR-0081 records (see the fixture
  README). `Http2FrameReaderTests` reads all eight frames from it, whole and one byte per read.
  Outbound tests pin the frames ADR-0081 sent to curl (empty `SETTINGS`, ACK, the server's
  `SETTINGS`, `HEADERS`/`CONTINUATION`, `DATA`, `PING` ACK, `RST_STREAM`, `GOAWAY`) plus
  `WINDOW_UPDATE`.
- Shape: one record per frame type deriving from `Http2Frame`, each writing its own payload;
  `Http2FrameEncoder` adds the header; `Http2FrameParser` (dispatch table by type, so no method
  passes complexity 10) and `Http2FrameReader` (bounded, `IConnection` or a read delegate, as
  `WebSocketFrameReader`). An unknown type is the `UnknownFrameSkipped` outcome with its header,
  not a frame value, so it can never be written; its payload is discarded through a 4 KiB buffer.
- Decisions taken (defaults, within ADR-0081): every frame size error is a connection error (ADR-0081
  decision 5 answers an oversized frame with `GOAWAY FRAME_SIZE_ERROR`; RFC 9113 section 5.4
  allows it); only `PRIORITY` length not 5, `PRIORITY` on itself and a stream `WINDOW_UPDATE` of 0
  are stream errors, since their whole payload is read and the next frame stays readable. Padding
  is checked as "fields after the padding must fit", which includes RFC 9113's "padding >= payload"
  case. A padded payload too short for the `Pad Length` byte and fixed fields is
  `FRAME_SIZE_ERROR` (section 4.2). `SETTINGS` values are checked per section 6.5.2
  (`ENABLE_PUSH`, `INITIAL_WINDOW_SIZE`, `MAX_FRAME_SIZE`); unknown identifiers and error codes are
  kept as numbers. A payload is allocated at its announced length, at most the advertised maximum
  (16384 per ADR-0081).
- Left to the connection (BL-368): a `HEADERS` whose priority names its own stream (must be
  judged after HPACK decodes the block, so HPACK state stays whole), `CONTINUATION` ordering, a
  client's `PUSH_PROMISE`, and splitting frames to the peer's `SETTINGS_MAX_FRAME_SIZE` - the
  encoder refuses only what 24 bits cannot say.
- Found: ADR-0081's reply "R" is described as `HEADERS` with `END_STREAM` then `DATA "hi"`, yet
  its stdout shows `hi`. Recorded here with exactly those flags, curl 8.21.0 ignores the `DATA`
  and prints only `HTTP/2 200 \r\n\r\n`; the ADR's run most likely sent `HEADERS` with
  `END_HEADERS` only. Worth correcting by BL-371 when it runs the exchange live. Not filed as a
  task because `Tasks/Backlog` is in BL-399's `touches` while it is in Doing.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. HTTP/2 frames are read (bounded, unknown types skipped) and written as typed values, every malformed frame mapped to its RFC 9113 error
