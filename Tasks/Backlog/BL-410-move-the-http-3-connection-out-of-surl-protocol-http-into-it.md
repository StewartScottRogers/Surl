---
id: BL-410
title: Move the HTTP/3 connection out of Surl.Protocol.Http into its own library
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-402, BL-384]
touches: [Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-410 — Move the HTTP/3 connection out of Surl.Protocol.Http into its own library

## Goal

The HTTP/3 frames, control and QPACK streams, `SETTINGS` and `GOAWAY` live in a horizontal library (planning name
`Surl.Http3`) that `Surl.Protocol.Http` and the WebTransport server both reference. `surl https://` answers
HTTP/3 byte for byte as before.

## Context

- Decision: BL-402's ADR, the HTTP/3 section. If the name differs, use the ADR's name and correct `touches`.
- What exists: BL-383 built HTTP/3 inside `Protocols/Http/Surl.Protocol.Http.UnitLibrary` on `Surl.Quic`
  (BL-373 to BL-379) and `Surl.Qpack` (BL-382), and BL-384 serves it from the executable.
- Extended CONNECT and HTTP datagrams are the next task (BL-411). This one only moves code.
- New projects go in `Protocols/Http/`, are listed in `Surl.slnx`, and the row joins `ProtocolIsolationTests`.

## Acceptance criteria

- [ ] The new library and its test project exist, are in `Surl.slnx`, and the moved types' tests moved with them.
- [ ] Every `Surl.Protocol.Http.UnitTests` test passes, with no expected byte edited.
- [ ] A named test in the new library's tests answers a request stream over the in-memory QUIC test connection
      with no `Surl.Protocol.Http` reference.
- [ ] `ProtocolIsolationTests` passes, `dotnet build -warnaserror` is clean, the fast tests are green, and both
      libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
