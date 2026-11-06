---
id: BL-410
title: Move the HTTP/3 connection out of Surl.Protocol.Http into its own library
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-402, BL-384]
touches: [Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.Http2.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Surl.slnx, Documentation/Planning/Decisions/ADR-0086-how-the-extended-servers-share-http-2-http-3-and-quic.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] The new library and its test project exist, are in `Surl.slnx`, and the moved types' tests moved with them.
- [x] Every `Surl.Protocol.Http.UnitTests` test passes, with no expected byte edited.
- [x] A named test in the new library's tests answers a request stream over the in-memory QUIC test connection
      with no `Surl.Protocol.Http` reference.
- [x] `ProtocolIsolationTests` passes, `dotnet build -warnaserror` is clean, the fast tests are green, and both
      libraries keep 100% line and branch coverage.

## Notes

- **Shape (ADR-0086 decision 2, settled in its Amendment 1).** `Surl.Http3.UnitLibrary` (namespace
  `Surl.Http3`; Abstractions, Quic, Qpack) holds the frames, control and QPACK streams, `SETTINGS`,
  `GOAWAY`, limits, `Http3ConnectionServer` (now public, constructed with an
  `IHttp3RequestStreamHandler`, `Http3ServerOptions` and the `ExchangeContext`), and the new
  `Http3RequestStream` / `Http3RequestHead` / `Http3RequestHeadMapping`. `Surl.Protocol.Http` keeps
  `Http3ProtocolServer`, `Http3ApplicationProtocol`, and the new adapters `Http3StreamAnswerWriter`
  (the stream handler) and `Http3HttpRequestBody` (renamed from `Http3RequestBody`; upload limit and
  `100-continue`).
- **Bytes kept.** The 431 the connection writes itself takes its `server` field from
  `Http3ServerOptions.ServerFieldValue` (the HTTP server sets `surl`). A content-store read failure
  uses `ResetResponseAsync` (RESET_STREAM alone) and the connection's post-handler
  `STOP_SENDING H3_NO_ERROR`, exactly the old sequence. All 78 HTTP/3 tests in
  `Surl.Protocol.Http.UnitTests` pass with no expected byte edited (one assertion's constructor call
  changed with the rename; the two `Http3DataStream` tests moved to `Surl.Http3.UnitTests`).
- **Head mapping copied, not shared**: `Surl.Http3` may not reference `Surl.Http2`; the copy and its
  tests live in `Surl.Http3`, log text unchanged. `Http2RequestHeadMapping`'s remark and
  `Surl.Http2`'s CLAUDE.md said HTTP/3 used it, so `Surl.Http2.UnitLibrary` joined `touches`
  (doc-comment edit only; no task in Doing names it), as did ADR-0086 and the decisions README for
  Amendment 1 (no task in Doing names them).
- **Test client split**: `Http3TestQuicClient` (protocol-neutral, in `Surl.Http3.UnitTests`, with
  `UnitTestQuicClient`, `UnitTestTls13Client`, `Http3TestResponse`) is compiled into
  `Surl.Protocol.Http.UnitTests` by link; `Http3TestClient` there subclasses it with the content
  store. The h3-client-initial fixture is embedded in both test projects under the same name.
- Named test: `Http3ConnectionServerTests.ServeAsync_RequestStream_IsHandedToTheHandlerWhichAnswersItWithNoHttpServer`
  (`Surl.Http3.UnitTests`).
- Coverage (Measure-CodeQuality, full fast run): `Surl.Http3.UnitLibrary` 100/100, `Surl.Protocol.Http.UnitLibrary`
  100/100, `Surl.Http2.UnitLibrary` 100/100, worst CRAP 10.
- Backlog BL-709 and BL-710 named types that moved; their Context now names the new homes, and
  BL-710's `touches` gained `Surl.Http3` (its `Http3DataStream` now lives there).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Http3 holds the HTTP/3 connection behind IHttp3RequestStreamHandler; surl https:// answers HTTP/3 with unchanged bytes
