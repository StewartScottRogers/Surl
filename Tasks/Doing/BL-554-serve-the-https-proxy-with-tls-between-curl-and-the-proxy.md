---
id: BL-554
title: Serve the HTTPS proxy with TLS between curl and the proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-552]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-554 — Serve the HTTPS proxy with TLS between curl and the proxy

## Goal

The HTTP proxy runs as an HTTPS proxy: curl's connection to the proxy is TLS from
`Surl.Networking`'s server TLS, with the ALPN BL-550's ADR decision 6 names, and forwarding and
`CONNECT` tunnelling (a TLS session to the origin inside the tunnel included) behave as over plain
TCP.

## Context

- Decision: BL-550's ADR, decision 6. The proxy library never constructs an `SslStream`; it runs on
  the `IConnection` that `Surl.Networking`'s `ServerTlsHandshake` produces (ADR-0010's server-side
  TLS contract, `TlsApplicationProtocols`).
- What curl sends: `-x https://localhost:<port>` with `--proxy-cacert` or `--proxy-insecure`
  (https://curl.se/docs/manpage.html, curl 8.21.0); measure the ClientHello's ALPN and the
  following bytes with `Record-CurlExchange.ps1` against the pinned builds.
- Upstream test cases that wait on this (tag `curl-8_21_0`), `https-proxy`: 1586, 1630, 1631,
  1632, 3401. BL-556 runs them.

## Acceptance criteria

- [ ] Named tests in `Surl.Protocol.HttpProxy.UnitTests` show the proxy reports TLS to the proxy in
      its exchange log as the ADR says, and relays a `CONNECT` tunnel's bytes unchanged when they
      are a nested TLS record stream.
- [ ] A named test in `Surl.Networking.UnitTests` completes a loopback TLS handshake offering the
      ADR's ALPN for the proxy and then exchanges one proxied request, without
      `TestCategory=Integration`.
- [ ] A proxy-side TLS failure (no shared ALPN, bad handshake) ends as ADR-0010 says, with a named
      test.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library
      keeps 100% line and branch coverage.

## Notes

- If `Surl.Networking`'s TLS code itself needs a change (not only a new test), add
  `Foundation/Surl.Networking.UnitLibrary` to `touches` before claiming, or file it as its own
  task.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
