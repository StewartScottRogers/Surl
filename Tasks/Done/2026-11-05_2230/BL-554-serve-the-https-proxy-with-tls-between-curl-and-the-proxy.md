---
id: BL-554
title: Serve the HTTPS proxy with TLS between curl and the proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-552]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary/ListenUrl.cs, Documentation/Planning/Decisions/ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] Named tests in `Surl.Protocol.HttpProxy.UnitTests` show the proxy reports TLS to the proxy in
      its exchange log as the ADR says, and relays a `CONNECT` tunnel's bytes unchanged when they
      are a nested TLS record stream.
- [x] A named test in `Surl.Networking.UnitTests` completes a loopback TLS handshake offering the
      ADR's ALPN for the proxy and then exchanges one proxied request, without
      `TestCategory=Integration`.
- [x] A proxy-side TLS failure (no shared ALPN, bad handshake) ends as ADR-0010 says, with a named
      test.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library
      keeps 100% line and branch coverage.

## Notes

- If `Surl.Networking`'s TLS code itself needs a change (not only a new test), add
  `Foundation/Surl.Networking.UnitLibrary` to `touches` before claiming, or file it as its own
  task.
- 2026-11-05 (BL-554): measured with `Record-CurlExchange.ps1 -Tls -TlsAlpn` (reference build and
  the pinned static 8.21.0 build): curl offers `http/1.1` to an HTTPS proxy, and `h2` first with
  `--proxy-http2`, which an `https` listener offering `h2, http/1.1` agrees (exit 16). So the
  proxy listener must offer `http/1.1` alone, which needed a change in `Surl.Networking` and a
  marker on the listen URL. Added to `touches`, none of them named by a task in `Doing`:
  `Foundation/Surl.Networking.UnitLibrary` (`TlsApplicationProtocols.ForListener`, used by
  `ServerTlsHandshake.ForListener`), `Foundation/Surl.Protocol.Abstractions.UnitLibrary/ListenUrl.cs`
  (init-only `IsProxy`, default `false`, so nothing else changes; BL-430 names only
  `HttpAuthenticationRefusal.cs` there), and the ADR-0088 file and the decisions index for
  Amendment 2. BL-555's Notes now say to set `IsProxy` on listen URLs from `--proxy`.
- Decided (ADR-0088 Amendment 2): the proxy notes `HTTPS proxy: curl's connection to the proxy is
  TLS (<protocol>, ALPN <id or none>); requests are read inside it.` once before reading, and closes
  unwritten a session that agreed an ALPN other than `http/1.1`. The handshake and its failure
  stay the serving engine's (ADR-0010 section 2), so a failed handshake is noted
  `TLS handshake failed: ...` and never reaches the proxy.
- Learned: on Windows, Schannel fails the handshake when ALPN was offered and none is in common
  ("No common application protocol exists"), unlike ADR-0010 section 6's table, which says it
  completes without ALPN. Recorded in Amendment 2; the test pins Windows and Linux, not macOS.
- Tests: `HttpProxyProtocolServerTlsTests` (the two new fixtures `https-proxy-forward` and
  `https-proxy-http2-offered`, ALPN none, `h2` closed, a plain connection unnoted, and a `CONNECT`
  tunnel relaying a real SslStream ClientHello and TLS records both ways unchanged);
  `ServerTlsHandshakeHttpsProxyTests` (proxy ALPN is `http/1.1` alone; an in-memory handshake
  offering `h2,http/1.1` or `http/1.1` agrees `http/1.1` and then serves curl's request through
  `HttpProxyProtocolServer`; only `h2` offered fails the handshake; plain HTTP to the HTTPS proxy
  is `TlsHandshakeException` with nothing written); `TlsApplicationProtocolsTests.ForListener_*`.
  `Surl.Networking.UnitTests` now references `Surl.Protocol.HttpProxy.UnitLibrary`.
- Coverage (`Measure-CodeQuality.ps1 -Library`): both libraries 100% line and branch. The script
  lists six complexity rows in `Surl.Protocol.HttpProxy.UnitLibrary`, all in BL-552's methods
  (`ProxyTarget.ParseAuthority`, `OriginResponseHead.ParseStatusLine`/`ParseFieldLine`,
  `OriginLink.ReadFieldsAsync`, `OriginResponseBody.Of`, `AnswerForwardableAsync`); the build's
  CA1502 gate passes, and none is code this task touched.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The HTTPS proxy: an https proxy listener offers ALPN http/1.1 only, the proxy notes TLS and forwards and tunnels inside it as over TCP
