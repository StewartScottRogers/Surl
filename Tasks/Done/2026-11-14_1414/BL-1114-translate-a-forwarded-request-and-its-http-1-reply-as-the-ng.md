---
id: BL-1114
title: Translate a forwarded request and its HTTP/1 reply as the nghttpx gateway half does
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1113]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: FR-053
created: 2026-11-14
completed: 2026-11-14
---
# BL-1114 — Translate a forwarded request and its HTTP/1 reply as the nghttpx gateway half does

## Goal

`Surl.Protocol.Http.UnitLibrary` holds the pure translation that BL-1113's ADR-0154 decides.
Given an HTTP/2 or HTTP/3 request head and body, it writes the HTTP/1.1 request the nghttpx
gateway forwards. Given the HTTP/1 reply bytes the case half sends back, it reads them into the
HTTP/2 and HTTP/3 response fields and body. It has no socket and no stream state.

## Context

- Read ADR-0154 (BL-1113) first. It names these types and every field rule. This task builds only
  the translation; BL-1115 (HTTP/2) and BL-1117 (HTTP/3) wire it to connections.
- Inputs already exist: `Http2RequestHead` and `Http2RequestHeadMapping` in
  `Protocols/Http/Surl.Http2.UnitLibrary`, the HTTP/3 request head in
  `Protocols/Http/Surl.Http3.UnitLibrary`, and `Surl.HttpMessage`'s response-head parsing.
  `Surl.Protocol.Http.UnitLibrary` already references all three.
- The expected bytes come from upstream's test cases at tag `curl-8_21_0`, imported under
  `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data`:
  - test 1700: the `<protocol>` request seen by the backend, after the case's own strips;
  - test 2403: the `<stdout>` response fields over HTTPS;
  - test 2500: the forwarded HTTP/3 request `GET https://localhost:%HTTP3PORT/2500 HTTP/1.1`.

  Copy each one into a unit test as a literal, with the case number in the test name.
- Like `HttpUpstreamTestCaseServer`, this code is never composed by the shipped `surl`
  (ADR-0080 decision 2). It uses no store and no event stream, so no stand-in hooks apply.
- Keep it to about 300 lines of production code. If ADR-0154 decides on more than that, stop at
  the request direction and file the response direction as a follow-up task.

## Acceptance criteria

- [x] The request translation named in ADR-0154 has unit tests in
      `Surl.Protocol.Http.UnitTests` that produce test 1700's forwarded request and test 2500's
      forwarded HTTP/3 request byte for byte, once the case's `<strip>` lines are applied.
- [x] The response translation turns test 2403's `<data>` reply into exactly the response fields
      its `<stdout>` lists, in order, and a unit test pins that.
- [x] Each field rule in ADR-0154 has a named test: added, removed, lower-cased, and a reply with
      no `Content-Length` (close-delimited).
- [x] `dotnet build Protocols/Http/Surl.Protocol.Http.UnitLibrary -warnaserror` is clean, and the
      library keeps 100% line and branch coverage.
- [x] No test needs `TestCategory=Integration`.

## Notes

Built NghttpxGatewayRequestTranslator, NghttpxGatewayResponseTranslator, NghttpxGatewayResponse and NghttpxGatewayVersion (about 230 lines). Inputs are plain values (method, scheme, authority, path, fields, body), not Http2RequestHead or Http3RequestHead, so BL-1115 and BL-1117 map their own heads and one translator serves both. Unpinned rules follow ADR-0154. A reply with no complete head returns null; an unreadable status line is 502. 34 new tests; both classes at 100% line and branch.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. NghttpxGatewayRequestTranslator and NghttpxGatewayResponseTranslator reproduce cases 1700, 2500 and 2403 byte for byte, at 100% coverage
