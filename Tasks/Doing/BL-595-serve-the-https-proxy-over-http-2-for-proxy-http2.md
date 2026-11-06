---
id: BL-595
title: Serve the HTTPS proxy over HTTP/2 for --proxy-http2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-554, BL-409]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Documentation/Planning/Decisions/ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md]
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
- 2026-11-06 (lane 6): `touches` widened. The ALPN the HTTPS proxy offers is not chosen in
  `Surl.Protocol.HttpProxy`: `Foundation/Surl.Networking.UnitLibrary`'s
  `TlsApplicationProtocols.ForListener` pins exactly `http/1.1` for an `https` listen URL with
  `ListenUrl.IsProxy`, and `ServerTlsHandshakeHttpsProxyTests` in `Surl.Networking.UnitTests`
  pin it; `ListenUrl.IsProxy`'s doc comment in `Foundation/Surl.Protocol.Abstractions.UnitLibrary`
  says the same and must change with it; and acceptance criterion 2 amends ADR-0088. Referencing
  `Surl.Http2.UnitLibrary` (and `Surl.Hpack.UnitLibrary`) from the proxy needs no ADR-0002 change:
  both are horizontal libraries every protocol server may reference
  (`ProtocolIsolationTests.HorizontalLibraries`), so they are read, not changed, and stay out of
  `touches`.
- Overlaps at the time: `Foundation/Surl.Protocol.Abstractions.UnitLibrary` with BL-901, and
  `Documentation/Planning/Decisions` (the whole folder) with BL-687. Back to Backlog until those
  are Done; no code was written.
- Plan for the run that picks it up: offer `h2` then `http/1.1` for an `https` proxy listener;
  in `HttpProxyExchange` accept ALPN `h2` and drive `Surl.Http2`'s connection with an
  `IHttp2StreamHandler` that answers a `CONNECT` stream (`:method CONNECT`, `:authority`, no
  `:scheme`/`:path`, RFC 9113 8.5) with `:status 200` and relays DATA both ways, and forwards
  other requests as the HTTP/1.1 path does. Measure first with `Record-CurlExchange.ps1 -Tls
  -TlsAlpn` against the static 8.21.0 build (`589C8E4D…2648`), extending the recorder to speak
  enough HTTP/2 if needed (the 2026-11-05 measurement ended exit 16 because it spoke none).

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Backlog. Needs Foundation/Surl.Protocol.Abstractions.UnitLibrary (held by BL-901) and Documentation/Planning/Decisions (held by BL-687); touches widened, restart once they are Done
- 2026-11-06: Backlog -> Doing.
