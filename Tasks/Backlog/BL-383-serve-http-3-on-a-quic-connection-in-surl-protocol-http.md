---
id: BL-383
title: Serve HTTP/3 on a QUIC connection in Surl.Protocol.Http
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-366, BL-378, BL-380, BL-382]
touches: [Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-383 — Serve HTTP/3 on a QUIC connection in Surl.Protocol.Http

## Goal

`Surl.Protocol.Http` serves HTTP/3 (RFC 9114) on a QUIC connection. It opens its control and QPACK
streams, exchanges `SETTINGS`, reads each request stream's `HEADERS` and `DATA` frames, answers through
BL-366's version-independent code, and ends with `GOAWAY` on shutdown and with the HTTP/3 error code
that each limit maps to.

## Context

- Decision: BL-372's ADR, decisions 2, 7 and 8.
- RFC 9114: section 4 (request and response mapping, pseudo-headers, malformed messages), 6.2
  (unidirectional stream types: control, push, and QPACK encoder and decoder), 7 (frames: `DATA`,
  `HEADERS`, `SETTINGS`, `GOAWAY`, `MAX_PUSH_ID`, reserved types ignored), and 8 (error codes).
- Building blocks: BL-378's stream API in `Surl.Quic`, BL-382's `Surl.Qpack`, BL-366's answering code,
  and BL-380's seam (the server implements the contract the ADR chose). Add the project references
  that the ADR's ADR-0002 rows allow.
- Content, authentication, uploads and log lines behave as over HTTP/1.1 and HTTP/2, except where the
  ADR says otherwise (for example, connection-bound logins).
- If HTTP/3 is registered as its own server in `ComposeProtocolServers`, that is BL-384's job. This task
  stops at the library.

## Acceptance criteria

- [ ] Over the in-memory QUIC test connection, a `GET` of a content-store file returns `HEADERS` and
      `DATA` frames whose decoded status, fields and body equal the HTTP/1.1 answer for the same file.
- [ ] Named tests cover: two concurrent request streams; a `POST` body under `--allow-uploads`; a
      header block over `--max-message` refused with the ADR's code; a malformed request (missing
      `:scheme`, a connection-specific field) answered with `H3_MESSAGE_ERROR`; a second `SETTINGS` on
      the control stream ending with `H3_FRAME_UNEXPECTED`; the client's control stream closing
      ending with `H3_CLOSED_CRITICAL_STREAM`; and `GOAWAY` on shutdown.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Protocol.Http` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
