---
id: BL-1733
title: Withhold DATA payloads on HTTP/2 CONNECT streams from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1713, BL-1714]
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1733 — Withhold DATA payloads on HTTP/2 CONNECT streams from the exchange log

## Goal

The exchange log withholds the payload of every `DATA` frame on an HTTP/2 `CONNECT` stream.

## Context

- Found while working BL-1713. `Http2RequestBytesRedactor` (`Protocols/Http/Surl.Http2.UnitLibrary`) logs `DATA`
  frames as received.
- `Protocols/Http/Surl.Protocol.HttpProxy.UnitTests/Fixtures/https-proxy-h2-connect/request.bin` (pinned upstream curl
  8.21.0) shows curl tunnelling plaintext HTTP/1.1 in `DATA` frames on its `CONNECT` stream. So
  `curl --proxy-http2 -p -x https://<proxy> -u a:s3cret http://<origin>/` logs the `Authorization` value. That is read
  from the fixture and the code (the recording itself sends no `-u`); no test reproduces it yet.
- Design, from the BL-1713 plan:
  - the redactor keeps the set of stream IDs whose decoded header block has `:method` `CONNECT`, also when that block
    is itself withheld for carrying `proxy-authorization`;
  - a `DATA` frame on such a stream, and any `DATA` frame on any stream once the HPACK decoder copy is lost, is written
    as its 9-byte frame header and then `****`, or the header alone when the payload is empty;
  - every such payload is withheld: no sniffing of its content and no redactor per stream.
- The same redactor serves the origin HTTP servers, gRPC, DNS over HTTPS and the SOCKS tunnel, so the rule holds for
  every `CONNECT` stream they log. The amendment says so.
- BL-1714 comes first: it makes the redactor mark the decoder lost after an oversized header frame.
- `HttpProxyProtocolServer.CreateRedactor`'s doc comment (from BL-1713) says these `DATA` frames are logged as
  received. That comment is the only change expected in `Surl.Protocol.HttpProxy.UnitLibrary`.
- Stand-in: only the levelled log and trace dumps change. The event stream keeps raw bytes (ADR-0182 decision 4), so a
  test still sees what the client sent; no store or fault is involved.

## Acceptance criteria

- [x] `Http2RequestBytesRedactorTests.Redact_DataOnAConnectStream_IsWithheld` and
      `Redact_EmptyDataOnAConnectStream_WritesTheHeaderAlone` pass.
- [x] `Http2RequestBytesRedactorTests.Redact_DataOnAnotherStreamBesideAConnect_IsLoggedAsReceived` passes.
- [x] `Http2RequestBytesRedactorTests.Redact_DataAfterTheDecoderIsLost_IsWithheld` passes.
- [x] `HttpProxyExchangeLogRedactionTests.H2ProxyConnection_CurlsConnectStreamData_IsWithheldFromTheLog` redacts
      `https-proxy-h2-connect/request.bin` and finds `GET /a HTTP/1.1` nowhere and the `DATA` frame header followed by
      `****`.
- [x] ADR-0182 gains an amendment stating the rule, and the doc comments of `Http2RequestBytesRedactor` and
      `HttpProxyProtocolServer.CreateRedactor` match it.
- [x] Build with `-warnaserror` is clean, fast tests are green, and `Surl.Http2.UnitLibrary` stays at 100% line and
      branch coverage.

## Notes

- Rule recorded as ADR-0182 amendment 11 (decided by Claude under Stewart's delegation).
- Choice beyond the plan: the set of `CONNECT` stream IDs is bounded. Past 4,096 streams every later `DATA` payload is
  withheld, so a client cannot grow it without limit. Pinned by
  `Redact_DataOnAnyStreamAfterMoreConnectStreamsThanAreRemembered_IsWithheld`.
- Accepted limit: `DATA` sent before its stream's `CONNECT` header block (not allowed to a client) is logged as received.
- `Redact_OtherFrameInsideAnOpenBlock_WithholdsTheHeldFramesAndLaterBlocks` now expects the interrupting `DATA` frame
  withheld, because it loses the decoder.
- Added `Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary` to `touches`: `SocksProtocolServer.CreateRedactor`'s doc
  comment said everything else is logged as it arrived, which the shared redactor's new rule made false. Doc comment
  only. No task in Doing named that project (only BL-1735, on `Application/Surl.Cli.*`).
- Help text: `--aihelp logging` and the manual are not changed here (`Application/Surl.Cli.*` was BL-1735's). The shift
  branch already holds BL-1745 (Backlog) to say there that these `DATA` payloads are withheld; until it is done, that
  text, as BL-1735 wrote it, says they are logged as received.
- Pipeline: the plan was this task's own `Context` design; tests (seen failing first), code and documents were written
  in the session, and `code-reviewer` reviewed them (nothing under Must fix; its wording findings are fixed). No
  conformance stage: no option, exit code or wire byte changes, only the log.
- Verified 2026-11-25: build `-warnaserror` 0 warnings, format clean, fast tests exit 0 over all 83 test assemblies,
  `Surl.Http2.UnitLibrary` and `Surl.Protocol.HttpProxy.UnitLibrary` 100% line and branch.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The exchange log withholds the DATA payloads of HTTP/2 CONNECT streams, and every DATA payload once the HPACK decoder copy is lost
