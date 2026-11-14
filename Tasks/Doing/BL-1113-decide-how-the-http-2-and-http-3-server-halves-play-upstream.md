---
id: BL-1113
title: Decide how the http/2 and http/3 server halves play upstream's nghttpx gateway
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1113 — Decide how the http/2 and http/3 server halves play upstream's nghttpx gateway

## Goal

A new ADR, ADR-0154, decided by Claude under Stewart's delegation, says how `Surl.Conformance`
plays the `http/2` and `http/3` server halves of upstream curl's test cases. It is detailed
enough that BL-1114 to BL-1119 can be built without another question.

## Context

- FR-053 (`Documentation/Product/Requirements.md`) and the Phase 6 milestone in
  `Documentation/Planning/Roadmap.md` both still say "No upstream test case runs over HTTP/2 or
  HTTP/3, since those server halves are not built". `UpstreamTestCaseServers.All`
  (`Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServers.cs`) has `http/2` and `http/3`
  as its only rows with `IsBuilt: false`. Every other server name the corpus uses is built.
- ADR-0080 decision 2's table says only "`http/2`, `http/3` | Phase 6's HTTP/2 and HTTP/3 servers,
  once built | verbatim reply". That does not fit what upstream runs. At tag `curl-8_21_0`,
  `tests/http2-server.pl` starts **nghttpx as a reverse proxy in front of the case's `http` server
  (`sws`)**: `--backend=127.0.0.1,<HTTPPORT>`, `--frontend="*,<HTTP2PORT>;no-tls"` and
  `--frontend="*,<HTTP2TLSPORT>"` with `certs/test-localhost.key` and `.pem`, plus
  `--conf=tests/nghttpx.conf` and `--backend-keep-alive-timeout=500ms`. `tests/http3-server.pl` does
  the same for `%HTTP3PORT` over QUIC. Read both scripts, `tests/nghttpx.conf` and `tests/servers.pm`
  at the tag (https://github.com/curl/curl/tree/curl-8_21_0/tests), and the nghttpx manual for what
  it adds to and removes from forwarded requests and responses.
- So what the cases verify is nghttpx's behaviour as well as curl's:
  - `<protocol>` is the HTTP/1.1 request **as forwarded to the backend**. For example, test 1700
    strips `^X-Forwarded-Proto:.*` and `^Via:.*`, and test 2500 shows the HTTP/3 request forwarded as
    `GET https://localhost:%HTTP3PORT/2500 HTTP/1.1`.
  - `<stdout>` shows the HTTP/2 or HTTP/3 response with lower-cased fields, `Connection:` dropped,
    and nghttpx's own `server: nghttpx` and `via: 1.1 nghttpx` (test 2400).
- The cases. These are the 16 cases at the tag that name `http/2` or `http/3`, checked 2026-11-14
  against `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data`:
  - Can run on a reference pin with the HTTP2 or HTTP3 feature: 1700, 1701 and 1702 (`h2c`, the
    `Upgrade: h2c` path); 2403 (`http/2 SSL headers-api`); 2500 and 2503 (`http/3 nghttpx-h3`).
  - Never run: 2400, 2401, 2406, 2501, 358 and 359 need `Debug`; 2402, 2404, 2407 and 2502 are libtests.
- The pins. The Windows reference build has no HTTP2 or HTTP3 feature, so these cases run on the
  Linux (`linux-x64`) and macOS (`osx-arm64`) reference pins only, in CI's `Upstream test cases` job.
- `UpstreamCurlBuildFeatures.For` never sets `nghttpx` or `nghttpx-h3`. Upstream's runtests sets
  them only when it finds nghttpx. Without them, 2500 and 2503 skip `feature-missing:nghttpx-h3`.
- Building blocks already in the solution:
  - The HTTP/1 case half: `HttpUpstreamTestCaseServer` and `HttpUpstreamTestCaseReplies` in
    `Protocols/Http/Surl.Protocol.Http.UnitLibrary`.
  - HTTP/2 and HTTP/3: `IHttp2StreamHandler` and `Http2ConnectionServer` in
    `Protocols/Http/Surl.Http2.UnitLibrary`; `IHttp3RequestStreamHandler` in
    `Protocols/Http/Surl.Http3.UnitLibrary`; `Http2CleartextUpgrade`, `Http2CleartextUpgradeHandler`
    and `Http3ProtocolServer` in `Surl.Protocol.Http.UnitLibrary`.
  - The loopback-only `SocketOriginConnector` that the `http-proxy` half tunnels through (BL-556).
  - The generated `test-localhost` and `test-ca` certificates (BL-682).
- Stand-in: the gateway half is composed only by `Surl.Conformance.UnitTests` (ADR-0080 decision 2:
  `Surl.Console` never composes a case seam). It uses no shared store, publishes nothing on an
  instance's event stream and has no fault rules. The ADR states this.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-0154-*.md` exists. It is marked "Decided by Claude under
      Stewart's delegation", amends ADR-0080 decision 2's `http/2` and `http/3` row, and ADR-0080
      gains an "Amended" line pointing to it. Both are listed in the decisions `README.md`.
- [ ] The ADR decides how the half forwards each request to the case's HTTP/1 half, citing the
      upstream files above at tag `curl-8_21_0`. It says whether forwarding goes over a loopback
      socket or in process, and the exact HTTP/1.1 request it writes: the request-target form,
      `Host`, and each field nghttpx adds or removes. It also decides the HTTP/3 request-target form
      that test 2500 shows.
- [ ] The ADR decides how a backend HTTP/1 reply becomes HTTP/2 and HTTP/3 response fields:
      lower-casing, dropped connection-specific fields, and `server: nghttpx` and `via: 1.1 nghttpx`
      either written or excluded as `test-server-choice` with the reason. It also decides how a
      backend close or `swsclose` ends the stream.
- [ ] The ADR decides the ports (`%HTTP2PORT` plaintext with prior knowledge and `Upgrade: h2c`,
      `%HTTP2TLSPORT` over TLS with ALPN `h2`, `%HTTP3PORT` over QUIC on UDP) and the certificate each
      serves. It decides whether the selection rules add `nghttpx` and `nghttpx-h3` when the half is
      composed, and which server-command row each server name gets.
- [ ] The ADR names the types and the project each lives in, matching BL-1114 to BL-1119, or it
      updates those tasks' Context in the same commit. It lists each of the six runnable cases above
      with the outcome it expects on the Linux and macOS reference pins.
- [ ] No source file changes. The ADR cites no Curl port output (ADR-0003).

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
