---
id: BL-1117
title: Serve the http/3 gateway half over QUIC, forwarding to the HTTP/1 case half
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1114]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: FR-053
created: 2026-11-14
completed: 2026-11-14
---
# BL-1117 — Serve the http/3 gateway half over QUIC, forwarding to the HTTP/1 case half

## Goal

`Surl.Protocol.Http.UnitLibrary` has the http/3 gateway server half named in ADR-0154. It answers
HTTP/3 request streams over QUIC by forwarding each request through BL-1114's translation to the
case's HTTP/1 half, and it answers with the translated reply, so tests 2500 and 2503 can pass.

## Context

- Read ADR-0154 (BL-1113) first: the request-target form (test 2500 shows
  `GET https://localhost:%HTTP3PORT/2500 HTTP/1.1` at the backend), how replies are mapped, and the
  type's name.
- Build on `IHttp3RequestStreamHandler` in `Protocols/Http/Surl.Http3.UnitLibrary` and see how
  `Http3ProtocolServer`, `Http3StreamAnswerWriter` and `Http3HttpRequestBody` in
  `Surl.Protocol.Http.UnitLibrary` serve a request stream today (ADR-0082). Forward through the same
  seam BL-1115 chose.
- Test 2500 runs `--cacert %CERTDIR/certs/test-ca.crt --http3 --resolve localhost:%HTTP3PORT:%HOSTIP`,
  so the certificate is the generated `test-localhost` one, issued by `test-ca` (BL-682). Wiring the
  certificate is BL-1119's job. This task tests the half at the request-stream seam.
- Composing it over UDP in the conformance run is BL-1119.
- Stand-in note: test-only, so no store, event stream or fault rules (ADR-0080 decision 2).

## Acceptance criteria

- [x] A unit test feeds the half test 2500's request as a QPACK-encoded HTTP/3 request stream. It
      asserts the forwarded HTTP/1.1 request that reaches a fake backend, byte for byte, and the
      `HEADERS` and `DATA` frames written back for 2500's `<data>`.
- [x] A unit test does the same for test 2503's reply and pins the response fields its `<stdout>`
      lists.
- [x] A backend that closes without a reply ends the request stream as ADR-0154 decides, and a unit
      test names that outcome.
- [x] `dotnet build Protocols/Http/Surl.Protocol.Http.UnitLibrary -warnaserror` is clean, and the
      library keeps 100% line and branch coverage.
- [x] No test needs `TestCategory=Integration`.

## Notes

- Named `Http3GatewayUpstreamTestCaseServer` (an `IDatagramProtocolServer`, ctor takes the connector, backend port, certificate chain and key; BL-1119 supplies the generated `test-localhost` certificate). Forwarding is `Http3GatewayStreamForwarder` over `NghttpxGatewayBackend`, as BL-1115 chose.
- Decision (within ADR-0154 decision 3): the 502 on HTTP/3 carries no `server` field, since HTTP/3 never adds it.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. http/3 gateway half built and unit tested
