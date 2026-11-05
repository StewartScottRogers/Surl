---
id: BL-748
title: Reset QUIC streams with RESET_STREAM_AT on a Surl.Quic connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-573]
touches: [Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-748 — Reset QUIC streams with RESET_STREAM_AT on a Surl.Quic connection

## Goal

A `Surl.Quic` server connection whose caller asks for it sends the empty `reset_stream_at` transport parameter,
reads the client's, and can reset the sending half of a stream with a `RESET_STREAM_AT` frame carrying a Reliable
Size, delivering every byte below that size before the reset takes effect; a connection whose caller does not ask
sends exactly what it sends today.

## Context

- Decision: ADR-0117 decision 0 (WebTransport over HTTP/3 draft -16, sections 3.1 and 4.4: a WebTransport server
  MUST send an empty `reset_stream_at` transport parameter and MUST reset WebTransport data streams with
  `RESET_STREAM_AT`, Reliable Size at least the stream header). ADR-0086 decision 4 puts everything a QUIC server
  needs below its application protocol in `Surl.Quic`.
- Specification: draft-ietf-quic-reliable-stream-reset, the revision draft-ietf-webtrans-http3-16 cites (its
  frame types, its transport parameter codepoint, and its rules: only after the peer sent the parameter, Reliable
  Size never above the Final Size, a later frame may only lower the Reliable Size, retransmission until
  acknowledged). Take every codepoint from that draft's text, never from the Curl port.
- Off by default, like BL-573's `max_datagram_frame_size`: the HTTP server's and DNS over QUIC's bytes do not change.
- Needed by BL-459 (WebTransport stream resets).

## Acceptance criteria

- [x] Named tests in `Surl.Quic.UnitTests` show: the transport parameter sent only when the caller asks; a
      `RESET_STREAM_AT` sent with the Reliable Size given, the bytes below it still delivered and retransmitted,
      the bytes above it not; a plain `RESET_STREAM` sent instead when the client did not send the parameter; a
      received `RESET_STREAM_AT` delivering the bytes below its Reliable Size to the reader before the reset; a
      received frame whose Reliable Size exceeds its Final Size closing the connection with the draft's error;
      a connection that does not ask sending byte for byte what it sends today.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Quic.UnitLibrary` keeps 100% line
      and branch coverage.

## Notes

- Filed by BL-457 (lane 2, 2026-11-05): WTH3 -16 makes `RESET_STREAM_AT` mandatory for WebTransport, and no task on
  the board built it.
- Spec (lane 4): WTH3 -16 cites draft-ietf-quic-reliable-stream-reset-09 (4 July 2026). From its text:
  `reset_stream_at` is transport parameter 0x1d, empty (non-empty is `TRANSPORT_PARAMETER_ERROR`);
  `RESET_STREAM_AT` is frame type 0x24 {Stream ID, Application Protocol Error Code, Final Size, Reliable
  Size}, application data space only; Reliable Size > Final Size is `FRAME_ENCODING_ERROR`; a larger
  Reliable Size later is ignored, another error code is `STREAM_STATE_ERROR`; `RESET_STREAM` equals Reliable Size 0.
- Choices (decided by Claude under Stewart's delegation; the ADR text is BL-765, because BL-557 held
  `Documentation/Planning/Decisions` in Doing):
  - Option `QuicServerConnectionOptions.ResetStreamAt`, false by default. The frame is *sent* only when it is
    set and the client sent the parameter; otherwise `QuicStream.ResetAt` is a plain `RESET_STREAM` (the
    draft only needs the peer's parameter, but gating on the option too keeps "does not ask" byte-identical).
  - Received while the option is off: the draft names no error, so it is what any unknown frame type is,
    `FRAME_ENCODING_ERROR` with frame type 0x24 (RFC 9000 section 12.4) - what the server sent before BL-748.
  - Sending: unsent bytes from the Reliable Size on are dropped with any FIN written; the frame waits until
    every byte below the Reliable Size is sent (so its Final Size never exceeds the client's credit) and
    carries Final Size = what was sent by then. One reset per sending part: a later `ResetAt`, `Reset` or
    `STOP_SENDING` sends nothing more (the draft allows, never requires, a lower second frame).
  - Retransmission: lost `STREAM` data of a reset stream goes again only below the Reliable Size, a frame
    straddling it cut without FIN; the `RESET_STREAM_AT` itself goes again as it was.
  - Receiving: bytes below the smallest Reliable Size stay readable; `ResetErrorCode` is set once `Read` has
    returned them all (so `QuicApplicationConnection.ReadAsync` returns them, then 0).
  - Added `QuicApplicationConnection.ResetAtAsync` for BL-459.
- Coverage (Cobertura, `dotnet test --collect "Code Coverage;Format=cobertura"`): `Surl.Quic.UnitLibrary`
  line 1, branch 1. Surl.Quic.UnitTests: 450 passed.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Quic sends reset_stream_at when asked and resets streams with RESET_STREAM_AT, delivering bytes below the Reliable Size both ways
