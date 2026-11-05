---
id: BL-411
title: Accept extended CONNECT and HTTP datagrams in the HTTP/3 library
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-410, BL-573]
touches: [Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-411 — Accept extended CONNECT and HTTP datagrams in the HTTP/3 library

## Goal

A server built on the HTTP/3 library can do four things: advertise `SETTINGS_ENABLE_CONNECT_PROTOCOL` and
`SETTINGS_H3_DATAGRAM`, accept an extended CONNECT whose `:protocol` it handles, exchange HTTP datagrams and
capsules for that request stream, and claim the WebTransport stream types. This is what the WebTransport server
needs.

## Context

- Decision: BL-402's ADR (extended CONNECT, datagrams, WebTransport stream hand-off).
- Specifications: RFC 9220 (Bootstrapping WebSockets with HTTP/3, which defines extended CONNECT for HTTP/3 by
  reference to RFC 8441); RFC 9297 (HTTP datagrams: the quarter stream ID, `SETTINGS_H3_DATAGRAM` 0x33, the
  Capsule Protocol); RFC 9221 (the QUIC DATAGRAM frame and the `max_datagram_frame_size` transport parameter,
  which `Surl.Quic` must send); and draft-ietf-webtrans-http3-16, section 4 (the bidirectional stream signal
  `0x41` and unidirectional stream type `0x54`, each followed by the session ID).
- The HTTP server itself does not enable extended CONNECT. Its `SETTINGS` bytes do not change unless the ADR says so.
- If `Surl.Quic` lacks the DATAGRAM frame, add it in a separate task filed through `task-planner` and depend on it.
  Do not widen this one.

## Acceptance criteria

- [ ] Named tests, each with bytes written from the RFC sections above:
      - a server that registers `:protocol` `webtransport` sends `SETTINGS` with `ENABLE_CONNECT_PROTOCOL = 1`
        and `H3_DATAGRAM = 1`;
      - an extended CONNECT for a registered protocol reaches its handler;
      - one for an unregistered protocol is answered as RFC 9220 says;
      - an HTTP datagram whose quarter stream ID names the request stream is delivered to it;
      - a capsule on the request stream is read with its type and length;
      - a `0x41` bidirectional stream and a `0x54` unidirectional stream are handed to the session they name.
- [ ] The HTTP server's `SETTINGS` are unchanged: every `Surl.Protocol.Http.UnitTests` expected byte is unedited.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
