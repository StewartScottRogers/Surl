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
completed:
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

- [ ] Named tests in `Surl.Quic.UnitTests` show: the transport parameter sent only when the caller asks; a
      `RESET_STREAM_AT` sent with the Reliable Size given, the bytes below it still delivered and retransmitted,
      the bytes above it not; a plain `RESET_STREAM` sent instead when the client did not send the parameter; a
      received `RESET_STREAM_AT` delivering the bytes below its Reliable Size to the reader before the reset; a
      received frame whose Reliable Size exceeds its Final Size closing the connection with the draft's error;
      a connection that does not ask sending byte for byte what it sends today.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Quic.UnitLibrary` keeps 100% line
      and branch coverage.

## Notes

- Filed by BL-457 (lane 2, 2026-11-05): WTH3 -16 makes `RESET_STREAM_AT` mandatory for WebTransport, and no task on
  the board built it.

## Log

- 2026-11-05: Created.
