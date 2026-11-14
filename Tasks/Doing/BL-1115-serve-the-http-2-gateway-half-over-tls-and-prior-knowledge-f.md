---
id: BL-1115
title: Serve the http/2 gateway half over TLS and prior knowledge, forwarding to the HTTP/1 case half
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1114]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1115 — Serve the http/2 gateway half over TLS and prior knowledge, forwarding to the HTTP/1 case half

## Goal

`Surl.Protocol.Http.UnitLibrary` has the http/2 gateway server half named in ADR-0154. It takes an
HTTP/2 connection, either over TLS with ALPN `h2` or plaintext with prior knowledge, forwards each
stream's request through BL-1114's translation to the case's HTTP/1 half, and answers the stream
with the translated reply.

## Context

- Read ADR-0154 (BL-1113) first. It decides:
  - whether forwarding goes over a loopback socket or in process. If over a socket, it goes through
    an injected connector, like the `http-proxy` half's loopback-only `SocketOriginConnector`
    (BL-556); the half never constructs a `Socket` itself (root `CLAUDE.md`);
  - how a backend close or `swsclose` ends the stream;
  - the type's name.
- Build on `Http2ConnectionServer` and `IHttp2StreamHandler` in `Protocols/Http/Surl.Http2.UnitLibrary`
  (ADR-0081, ADR-0090). Look at how `Surl.Protocol.Http.UnitLibrary`'s own HTTP/2 path drives them:
  `Http2StreamAnswerWriter` and `Http2HttpRequestBody`.
- The `Upgrade: h2c` path is BL-1116. Composing this half in the conformance run is BL-1118.
- It is never composed by the shipped `surl` (ADR-0080 decision 2). It uses no store, publishes
  nothing on the event stream and has no fault rules, because it exists only for upstream's cases.
- Unit tests drive it over an in-memory `IConnection` with HTTP/2 frames written from RFC 9113 and
  the case bytes. The fast tests open no socket (NFR-001).

## Acceptance criteria

- [ ] A unit test writes the client preface, `SETTINGS` and a `HEADERS` frame for `GET /2403`. It
      asserts the forwarded HTTP/1.1 request that reaches a fake backend, and the `HEADERS` and
      `DATA` frames sent back for test 2403's `<data>` reply.
- [ ] A request with a body (`POST`) is forwarded with that body. A unit test pins the frames of
      both directions.
- [ ] Two streams on one connection are each forwarded and answered on their own stream ID, and a
      unit test pins this.
- [ ] A backend that closes without a reply ends the stream as ADR-0154 decides, and a unit test
      names that outcome.
- [ ] `dotnet build Protocols/Http/Surl.Protocol.Http.UnitLibrary -warnaserror` is clean, and the
      library keeps 100% line and branch coverage.
- [ ] No test needs `TestCategory=Integration`.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
