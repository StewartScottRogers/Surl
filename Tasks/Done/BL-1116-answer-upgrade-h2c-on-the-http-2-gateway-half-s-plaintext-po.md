---
id: BL-1116
title: Answer Upgrade: h2c on the http/2 gateway half's plaintext port
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1115]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: FR-053
created: 2026-11-14
completed: 2026-11-14
---
# BL-1116 — Answer Upgrade: h2c on the http/2 gateway half's plaintext port

## Goal

On its plaintext port, the http/2 gateway half from BL-1115 answers an HTTP/1.1 request carrying
`Upgrade: h2c` as nghttpx does, following ADR-0154. It upgrades with `101 Switching Protocols` and
answers stream 1 over HTTP/2, so tests 1700, 1701 and 1702 can pass on a pin with `h2c`.

## Context

- Read ADR-0154 (BL-1113), and ADR-0091 for how Surl's own server turns `Upgrade: h2c` into stream 1.
  Reuse `Http2CleartextUpgrade` and `Http2CleartextUpgradeHandler` in
  `Protocols/Http/Surl.Protocol.Http.UnitLibrary` rather than writing a second upgrade parser.
- Cases at tag `curl-8_21_0`, in `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data`:
  - 1700: two URLs on one command line. `--http2` applies to both, so the first request carries
    `Upgrade: h2c` and becomes stream 1; the second URL is stream 3 of the same connection
    (ADR-0154, "Correction to BL-1116's text").
  - 1701: a `POST` with an upgrade.
  - 1702: a `HEAD` with an upgrade.

  Each one's `<stdout>` shows the `101 Switching Protocols` head and the HTTP/2 response.
- A plaintext request with no `Upgrade` and no preface is forwarded as HTTP/1.1, as ADR-0154
  decides. No runnable case sends one; a unit test pins it.
- Same stand-in note as BL-1115: test-only, so no store, event stream or fault rules.

## Acceptance criteria

- [x] A unit test replays test 1700's two requests on one connection. The first, with
      `Upgrade: h2c`, gets the `101` head that `<stdout>` shows, then stream 1 answered with
      `<data>`; the second, stream 3, is answered with `<data1>`.
- [x] A unit test forwards a plaintext HTTP/1.1 request with no `Upgrade` as HTTP/1.1.
- [x] A unit test replays test 1701's `POST` upgrade. The forwarded request carries the body.
- [x] A unit test replays test 1702's `HEAD` upgrade. Stream 1 carries no `DATA`.
- [x] `dotnet build Protocols/Http/Surl.Protocol.Http.UnitLibrary -warnaserror` is clean, and the
      library keeps 100% line and branch coverage.
- [x] No test needs `TestCategory=Integration`.

## Notes

- Built: `Http2GatewayCleartextUpgrade` (the plaintext HTTP/1.1 handler: `101` and stream 1 for an `Upgrade: h2c` request, HTTP/1.1 passthrough otherwise), `Http2GatewayStreamForwarder` and `NghttpxGatewayBackend` (the per-stream forwarding and backend connection moved out of BL-1115's server, same behaviour). `Http2CleartextUpgrade.ClientSettingsOf` gained `allowsRequestBody` (default false, so Surl's own server is unchanged) for test 1701's `POST`.
- The upgrade request's body is read before the `101` and handed to stream 1; one that cannot be read whole is answered `400` (unpinned).
- Unpinned choices: the HTTP/1.1 passthrough drops the backend's `content-length` (the writer frames the body), so a passed-through `HEAD` shows `Content-Length: 0`; a reply cut mid-body aborts the connection. A TLS connection with no preface is still closed with no bytes.
- Pre-existing, not from this task: `NghttpxGatewayReplyFramingReader` measures 97.2% branch coverage (BL-1115); left alone.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. the http/2 gateway half answers Upgrade: h2c with 101 and stream 1 (tests 1700-1702) and forwards plain HTTP/1.1
