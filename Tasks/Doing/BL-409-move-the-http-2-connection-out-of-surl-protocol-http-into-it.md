---
id: BL-409
title: Move the HTTP/2 connection out of Surl.Protocol.Http into its own library
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-402, BL-369, BL-370]
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-409 — Move the HTTP/2 connection out of Surl.Protocol.Http into its own library

## Goal

The HTTP/2 frames, streams, flow control, `SETTINGS`, `PING` and `GOAWAY` handling live in a horizontal library
(planning name `Surl.Http2`) that both `Surl.Protocol.Http` and the gRPC server reference. `surl http://` and
`https://` answer HTTP/2 byte for byte as before.

## Context

- Decision: BL-402's ADR, the HTTP/2 section: the library name, what moves, its ADR-0002 row, and how a
  server receives a request stream and writes response headers, data and trailers. If the name differs, use the
  ADR's name and correct `touches`.
- What exists: BL-367 (frame codec), BL-368 (prior knowledge), BL-369 (`Upgrade: h2c`) and BL-370 (ALPN `h2`)
  built HTTP/2 inside `Protocols/Http/Surl.Protocol.Http.UnitLibrary`, and HPACK is in `Surl.Hpack` (BL-365).
- gRPC needs trailers: a final `HEADERS` frame with `END_STREAM` after the `DATA` frames (RFC 9113 section 8.1).
  If the HTTP server never sent trailers, the library gains that ability here, with a test.
- New projects go in `Protocols/Http/`, are listed in `Surl.slnx`, and the row joins `ProtocolIsolationTests`.

## Acceptance criteria

- [ ] The new library and its test project exist, are in `Surl.slnx`, and the moved types' tests moved with them.
- [ ] Every `Surl.Protocol.Http.UnitTests` test passes, with no expected byte edited.
      `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~Http2"` passes where a
      pinned HTTP/2 build exists.
- [ ] A named test in the new library's tests answers a stream with `HEADERS`, `DATA`, and a trailing `HEADERS`
      carrying `END_STREAM`, decoded and checked against RFC 9113 section 8.1. It uses no `Surl.Protocol.Http`
      reference.
- [ ] `ProtocolIsolationTests` passes, `dotnet build -warnaserror` is clean, the fast tests are green, and both
      libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
