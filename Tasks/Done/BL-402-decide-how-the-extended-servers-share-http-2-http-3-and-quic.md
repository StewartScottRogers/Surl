---
id: BL-402
title: Decide how the extended servers share HTTP/2, HTTP/3 and QUIC
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401, BL-364, BL-372]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-402 — Decide how the extended servers share HTTP/2, HTTP/3 and QUIC

## Goal

An ADR decides how gRPC (HTTP/2), WebTransport (HTTP/3 with extended CONNECT and HTTP datagrams) and DNS over
QUIC reach the hand-built HTTP/2, HTTP/3 and QUIC code without referencing `Surl.Protocol.Http`. It names the
libraries that are extracted and the tasks that extract them.

## Context

- BL-364's ADR plans HTTP/2 frames and the connection inside `Protocols/Http/Surl.Protocol.Http.UnitLibrary`,
  with HPACK in `Surl.Hpack`. BL-372's ADR plans `Surl.Quic`, `Surl.Tls13` and `Surl.Qpack` as their own
  libraries, and the HTTP/3 frames and server inside `Surl.Protocol.Http`. Read both ADRs as accepted. If
  either already puts the framing in a horizontal library, say so and shrink the extraction to match.
- Isolation (BL-543's ADR): an extended server may not reference `Surl.Protocol.Http`. So the HTTP/2
  connection (frames, streams, flow control, `SETTINGS`, `GOAWAY`) and the HTTP/3 connection (control and
  QPACK streams, frames) move to horizontal libraries that `Surl.Protocol.Http` and the extended servers both
  reference. Planning names: `Surl.Http2` and `Surl.Http3`, in `Protocols/Http/`.
- What the extended servers need beyond what the HTTP server uses:
  - **gRPC** (grpc/grpc `doc/PROTOCOL-HTTP2.md`) needs `h2c` prior knowledge for `grpc` and ALPN `h2` for `grpcs`,
    trailers (a `HEADERS` frame with `END_STREAM` after the `DATA`), `RST_STREAM`, and `PING`.
  - **WebTransport** (draft-ietf-webtrans-http3-16) needs `SETTINGS_ENABLE_CONNECT_PROTOCOL` (RFC 9220),
    extended CONNECT with `:protocol = webtransport`, HTTP datagrams (RFC 9297, `SETTINGS_H3_DATAGRAM`) over QUIC
    datagrams (RFC 9221, the `max_datagram_frame_size` transport parameter), capsules, and
    `SETTINGS_WT_MAX_SESSIONS`. It also needs WebTransport stream types (bidirectional signal `0x41`,
    unidirectional type `0x54`), which an HTTP/3 server must hand to the session they name.
  - **DNS over QUIC** (RFC 9250) needs QUIC streams on ALPN `doq` with no HTTP/3 at all, so it rides
    `Surl.Quic` directly.
- Decide, for each of HTTP/2 and HTTP/3: what moves, the library's ADR-0002 row, how a server registers a
  handler for a stream (and for extended CONNECT), and how ALPN chooses between HTTP and an extended server
  sharing a port. Planning assumption: they never share a port, because each listen URL is its own listener.

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It names each library, what moves into it type by type, its
      ADR-0002 row, and the extraction task (BL-409, BL-410, BL-411). It says how
      DNS over QUIC uses `Surl.Quic`, and is listed in the folder's `README.md`.
- [x] ADR-0002, BL-364's ADR and BL-372's ADR carry "Amended by" lines pointing to it, so none of them still
      says the framing lives only in `Surl.Protocol.Http`.

## Notes

- ADR-0086 decides it. ADR-0081 (BL-364) and ADR-0082 (BL-372) put no HTTP/2 or HTTP/3 framing in a
  horizontal library, so nothing shrinks: `Surl.Http2` (Abstractions, `Surl.Hpack`; BL-409) and `Surl.Http3`
  (Abstractions, `Surl.Quic`, `Surl.Qpack`; BL-410) are extracted as planned, and BL-411 adds extended
  CONNECT, HTTP datagrams, capsules and claimed stream types to `Surl.Http3`.
- Extended servers get stream-level handlers (`IHttp2StreamHandler`, `IHttp3RequestStreamHandler`,
  `IHttp3ExtendedConnectHandler`), not ADR-0084's `IHttpRequestHandler`: an `HttpAnswer` is decided whole, so
  it cannot carry gRPC's full-duplex streaming or trailers. The HTTP server adapts `HttpAnswer` onto them.
- DNS over QUIC needs a flow loop and per-caller ALPN that ADR-0082 did not give `Surl.Quic` (it fixed ALPN
  `h3` and no datagram extension, and left the loop for BL-383 inside `Surl.Protocol.Http`). Filed BL-546 (flow
  loop, `IQuicApplicationProtocol`, caller's ALPN and transport parameters; `Surl.Quic` gains a reference to
  Abstractions) and BL-547 (RFC 9221 DATAGRAM frames). Added BL-546 to BL-383's and BL-484's `depends-on`,
  and BL-547 to BL-411's, so none of them builds a second loop or lacks DATAGRAM frames.
- Also amended ADR-0084 (it left every HTTP/2 and HTTP/3 type in `Surl.Protocol.Http` until BL-402 decided),
  inside this task's `touches`.
- Kept build-then-extract rather than re-filing BL-367 to BL-370 and BL-383 to build straight into the new
  libraries: those tasks are already sized and ordered, and the move is proved by unedited expected bytes.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. ADR-0086 decides Surl.Http2, Surl.Http3 and Surl.Quic's flow loop for gRPC, WebTransport and DNS over QUIC; BL-546 and BL-547 filed
