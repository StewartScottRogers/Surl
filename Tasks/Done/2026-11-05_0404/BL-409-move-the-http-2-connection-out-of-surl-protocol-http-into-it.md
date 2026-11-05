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
completed: 2026-11-05
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

- [x] The new library and its test project exist, are in `Surl.slnx`, and the moved types' tests moved with them.
- [x] Every `Surl.Protocol.Http.UnitTests` test passes, with no expected byte edited.
      `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~Http2"` passes where a
      pinned HTTP/2 build exists.
- [x] A named test in the new library's tests answers a stream with `HEADERS`, `DATA`, and a trailing `HEADERS`
      carrying `END_STREAM`, decoded and checked against RFC 9113 section 8.1. It uses no `Surl.Protocol.Http`
      reference.
- [x] `ProtocolIsolationTests` passes, `dotnet build -warnaserror` is clean, the fast tests are green, and both
      libraries keep 100% line and branch coverage.

## Notes

What was built (2026-11-05):

- `Protocols/Http/Surl.Http2.UnitLibrary` (namespace `Surl.Http2`, references Abstractions and Hpack only) and
  `Surl.Http2.UnitTests`, listed in `Surl.slnx` under `/Protocols/Http/`. 36 `Http2*` types moved; the codec's
  tests (encoder, parser, reader, writer, malformations, send windows, data stream, head mapping) moved with them.
- New public surface per ADR-0086 decision 1: `IHttp2StreamHandler`, `Http2RequestStream` (`Head`,
  `ReadBodyAsync`, `WriteHeadersAsync`, `WriteDataAsync`, `CreateResponseBodyStream`, `WriteTrailersAsync`,
  `ResetAsync`), `Http2SettingsPayload` (the `HTTP2-Settings` decode `Http2CleartextUpgrade` used the internal
  parser for). Public too: `Http2ConnectionServer`, `Http2ConnectionPreface`, `Http2RequestHead`,
  `Http2RequestHeadMapping` (HTTP/3 in `Surl.Protocol.Http` still uses it until BL-410), `Http2DataStream`,
  `Http2Setting`, `Http2SettingIdentifier`, `Http2ErrorCode`. The rest stays internal.
- `Surl.Protocol.Http` keeps `Http2StreamAnswerWriter`, now its `IHttp2StreamHandler` (builds the `HttpRequest`,
  has `HttpRequestAnswerer` answer, resets `PROTOCOL_ERROR` on a content-length mismatch, writes the answer), and
  the new `Http2HttpRequestBody` (the `IHttpRequestBody` adapter: upload limit and `100-continue`, which are HTTP
  server policy, moved out of the library's body).
- Trailers test: `Http2ConnectionServerTests.ServeAsync_HandlerAnsweringWithHeadersDataAndTrailers_WritesThemInOrderEndingOnTheTrailers`
  and `Http2RequestStreamTests.WriteTrailersAsync_AfterHeadersAndData_EndsTheStreamOnATrailingHeadersAsRfc9113Section81Says`.
- Results: all fast tests green (`Surl.Http2.UnitTests` 156, `Surl.Protocol.Http.UnitTests` 439); conformance
  `~Http2` 15 passed, 1 skipped by its own `[OSCondition]` (Linux/macOS only); `Measure-CodeQuality.ps1`:
  both libraries 100% line, 100% branch, 0 failing members.

Choices made (sensible defaults, Decided by Claude under Stewart's delegation; ADR-0086 already decides the
design, and `Documentation/Planning/Decisions` is held by BL-503, so these are recorded here and in the two
projects' `CLAUDE.md` rather than as an ADR amendment):

- `Surl.Http2` grants `InternalsVisibleTo` to `Surl.Protocol.Http.UnitTests` as well as its own tests: the
  HTTP server's HTTP/2 tests replay upstream curl's bytes and encode and parse frames with the codec, and ADR-0086
  keeps the codec internal. Test-only; production isolation is unchanged and `ProtocolIsolationTests` holds it.
- The HTTP/2 test client (`Http2TestClient`, `Http2TestResponse`, `Http2TestBytes`, `Http2TestStream`) lives in
  `Surl.Http2.UnitTests` and is compiled into `Surl.Protocol.Http.UnitTests` by `<Compile Link>`, so there is one
  copy. `Surl.Http2.UnitTests` embeds the one recorded fixture its reader test uses (`h2-prior-knowledge-get`)
  from `Surl.Protocol.Http.UnitTests/Fixtures`.
- `Http2ConnectionServer` keeps its connection in the constructor (as built by BL-368) rather than ADR-0086's
  sketch of `ServeAsync(IConnection)`; the ADR's own caveat lets the building task name the shape.
- The library's head-timeout clamp is its own internal `Http2HeadTimeoutDelay`, a copy of `Surl.Protocol.Http`'s
  `HeadTimeoutDelay`, since `Surl.Http2` may not reference the HTTP server.
- A handler's `ResetAsync` goes through the connection, which also ends the request body; so a content-store
  file that fails mid-answer while the client's body was still arriving now gets only its `RST_STREAM
  INTERNAL_ERROR`, not a second `RST_STREAM NO_ERROR` on the already-reset stream. No test pinned the old pair
  and every recorded exchange is unchanged.
- Not added: `RequestTrailers` (a request's trailing header block is still accepted as the end of the body and its
  fields dropped, as before). gRPC clients send no request trailers; the gRPC task can add them if its ADR needs
  them.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Http2 holds the HTTP/2 connection behind IHttp2StreamHandler, with trailers; surl http:// and https:// answer HTTP/2 byte for byte as before
