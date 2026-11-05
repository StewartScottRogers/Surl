---
id: BL-383
title: Serve HTTP/3 on a QUIC connection in Surl.Protocol.Http
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-366, BL-378, BL-380, BL-382, BL-572]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] Over the in-memory QUIC test connection, a `GET` of a content-store file returns `HEADERS` and
      `DATA` frames whose decoded status, fields and body equal the HTTP/1.1 answer for the same file.
- [x] Named tests cover: two concurrent request streams; a `POST` body under `--allow-uploads`; a
      header block over `--max-message` refused with the ADR's code; a malformed request (missing
      `:scheme`, a connection-specific field) answered with `H3_MESSAGE_ERROR`; a second `SETTINGS` on
      the control stream ending with `H3_FRAME_UNEXPECTED`; the client's control stream closing
      ending with `H3_CLOSED_CRITICAL_STREAM`; and `GOAWAY` on shutdown.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Protocol.Http` keeps
      100% line and branch coverage.

## Notes

- **Built:** `Http3ConnectionServer` (an `IQuicApplicationProtocol`), `Http3RequestStreamServer`,
  `Http3RequestBody`, `Http3StreamReader`, `Http3FieldSections`, `Http3FrameWriter`,
  `Http3FrameType`, `Http3FrameLimit`, `Http3Settings`, `Http3DataStream`, `Http3ErrorCode` and the
  two exceptions, all internal. `Surl.Protocol.Http` now references `Surl.Quic` and `Surl.Qpack`
  (ADR-0082 decision 2's rows; `ProtocolIsolationTests` allows both).
- **Acceptance tests** (`Http3ConnectionServerTests.cs`):
  `ServeAsync_GetOfAContentStoreFile_AnswersWhatHttp11AnswersForTheSameFile`,
  `..._TwoConcurrentRequestStreams_AnswersEachIndependently`,
  `..._PostBodyUnderAllowUploads_AnswersWhatHttp11AnswersAndStopsTheUnreadBody`,
  `..._HeadersFrameLongerThanMaxMessage_Answers431ThenStopsAndTheConnectionCarriesOn`,
  `..._MalformedRequest_ResetsAndStopsTheStreamWithMessageError` (missing `:scheme`, `connection`),
  `..._SecondSettingsOnTheControlStream_ClosesWithFrameUnexpected`,
  `..._ClientClosesItsControlStream_ClosesWithClosedCriticalStream`,
  `..._ExchangeCancelledAtShutdown_SendsGoAwayNamingTheNextStreamThenClosesWithNoError`. 58 more
  in the `.Errors`, `.RequestStreams` and `.Answers` parts cover the rest; 68 HTTP/3 tests, 572 in
  the project.
- **"The ADR's code" for an over-long header block** is ADR-0082 decision 7's: `:status 431` with
  FIN, then `STOP_SENDING` `H3_NO_ERROR`.
- **The POST under `--allow-uploads`:** the HTTP server writes no uploads yet (its answerer answers
  `POST` `405` with `Allow: GET, HEAD` over every version), so the test pins that the HTTP/3 answer
  is HTTP/1.1's and the unread body is stopped `H3_NO_ERROR`; uploads arrive with the answerer's
  upload work, not here.
- **The in-memory QUIC test connection:** `Http3TestClient` drives the real `QuicDatagramFlowServer`
  over `InMemoryDatagramFlow`. Its QUIC side, `UnitTestQuicClient` and `UnitTestTls13Client`, is
  copied from `Surl.Quic.UnitTests` (test code, public `Surl.Quic` API only), and it starts from the
  static-8.21.0-win Initials (`Fixtures/h3-client-initial`, a copy of BL-363's recording). The HTTP/3
  frames sent after the handshake are written from RFC 9114 and 9204: no curl has sent a request
  stream to surl yet - the recorder answers nothing - and BL-385 measures that against a live surl.
- **Decisions** where ADR-0082 is silent (shutdown cuts streams off, a stream ending before
  `HEADERS`, the unread-body `STOP_SENDING`, the `431`'s fields, unknown stream types, the client's
  uni-stream limit, QPACK stream errors, `SETTINGS` errors) are recorded as ADR-0082 Amendment 1,
  "Decided by Claude under Stewart's delegation". Amended in place rather than a new ADR so no
  number races another lane.
- **Touches:** added `Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md`
  for that amendment; no task in Doing names it (BL-614, BL-619, BL-633, BL-693 checked).
- **Shared head mapping:** `Http2RequestHeadMapping` maps HTTP/3 heads too (RFC 9114 sections
  4.2-4.3 repeat RFC 9113 8.3); its connection-specific-field note now says "HTTP/2 or HTTP/3", and
  `Http2RequestHeadMappingTests` follows.
- **A reset discards what was not read** (`QuicStream.ReceiveReset`), so only a clean `FIN` ends a
  stream; a reset anywhere is `Http3StreamAbandonedException`. Found when the reset tests raced.
- **Filed:** BL-697 (`H3_VERSION_FALLBACK` for NTLM and Negotiate, after BL-602) and BL-698
  (hold answer bodies back until QUIC credit allows; `QuicApplicationConnection.WriteAsync` queues
  everything today).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Protocol.Http serves HTTP/3 on a Surl.Quic connection: control and QPACK streams, SETTINGS, request streams answered through HttpRequestAnswerer, ADR-0082's limits and GOAWAY on shutdown
