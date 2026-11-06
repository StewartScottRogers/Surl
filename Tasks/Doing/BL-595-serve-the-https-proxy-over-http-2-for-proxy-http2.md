---
id: BL-595
title: Serve the HTTPS proxy over HTTP/2 for --proxy-http2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-554, BL-409]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-595 — Serve the HTTPS proxy over HTTP/2 for --proxy-http2

## Goal

The HTTPS proxy offers ALPN `h2` and serves `CONNECT` (RFC 9113 section 8.5) and forwarded requests
over HTTP/2 to a curl given `--proxy-http2`, instead of ADR-0088's interim ALPN `http/1.1` only.

## Context

- Decision: ADR-0088 decision 6. Until this task the HTTPS proxy offers only `http/1.1`, which curl
  accepts and falls back to (measure that first with `Record-CurlExchange.ps1 -Tls -TlsAlpn` against
  a pinned build carrying the `HTTP2` feature; the Windows reference build has none, so use the static
  8.21.0 build ADR-0081 names).
- Builds on `Surl.Http2` (ADR-0086, BL-409): an `IHttp2StreamHandler` in `Surl.Protocol.HttpProxy`
  answers each stream; a `CONNECT` stream becomes a tunnel carried in `DATA` frames.

## Acceptance criteria

- [ ] Named tests in `Surl.Protocol.HttpProxy.UnitTests` pin the ALPN choice, a `CONNECT` stream's
      `:status 200` and its relayed bytes, and a forwarded request's response, from bytes measured
      against a pinned upstream curl build.
- [ ] ADR-0088 decision 6 is amended to say `h2` is offered.
- [ ] `dotnet build` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-550. Touches gains `Protocols/Http/Surl.Http2.UnitLibrary` only if a reference is
  added, which ADR-0002's table must then allow.

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
