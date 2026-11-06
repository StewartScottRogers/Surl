---
id: BL-940
title: Serve CONNECT and forwarded requests over HTTP/2 in Surl.Protocol.HttpProxy when TLS agreed h2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-939]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-940 — Serve CONNECT and forwarded requests over HTTP/2 in Surl.Protocol.HttpProxy when TLS agreed h2

## Goal

When a connection's TLS handshake agreed ALPN `h2`, `Surl.Protocol.HttpProxy` serves it as HTTP/2: a
`CONNECT` stream (RFC 9113 section 8.5) is answered `:status 200` and its tunnel relayed in `DATA`
frames, and any other request is forwarded to its origin as the HTTP/1.1 path forwards it, its response
relayed back on the stream.

## Context

- Second-to-last of the tasks BL-595 ("Serve the HTTPS proxy over HTTP/2 for --proxy-http2") was split
  into on 2026-11-06 after it timed out at 120 minutes. Needs BL-939 (`Http2RequestHead.Scheme` and
  `InternalsVisibleTo Surl.Protocol.HttpProxy.UnitTests`). The listener does not offer `h2` to a proxy
  until the task that depends on this one; here the choice is driven from `TlsSession.ApplicationProtocol`
  in unit tests, so no listener change is needed.
- Decision: ADR-0088 decision 6 (the HTTPS proxy); built on `Surl.Http2` (ADR-0086, BL-409):
  `Http2ConnectionServer` with an `IHttp2StreamHandler`. `Surl.Http2` and `Surl.Hpack` are horizontal
  libraries (`ProtocolIsolationTests.HorizontalLibraries`), so referencing them needs no ADR-0002 change.
- The work is written and its tests passed (293 proxy tests) when the run timed out during the coverage
  measurement. Take it from the stash `darkfactory BL-595 20261106-033059` in the main checkout
  (`Z:\repos\Surl`; find its index N with `git stash list`, matching the message; never pop, apply or
  drop it). None of these files has moved on the branch since the stash's base `a84caea4`.
  - Tracked changes, with `git diff "stash@{N}^" "stash@{N}" -- Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary Protocols/Http/Surl.Protocol.HttpProxy.UnitTests | git apply`:
    `HttpProxyExchange.cs` (serves `h2` first; `FixedText` internal), `HttpProxyExchange.Forwarding.cs`,
    `ProxyBodyCopier.cs`, `Surl.Protocol.HttpProxy.UnitLibrary.csproj` (references `Surl.Hpack` and
    `Surl.Http2`), `HttpProxyProtocolServerTlsTests.cs` (the refused-ALPN test now uses `spdy/3.1`),
    `Surl.Protocol.HttpProxy.UnitTests.csproj` (the new fixtures copied to output).
  - Untracked files, each with `git checkout "stash@{N}^3" -- <path>`:
    `Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary/AlreadyReadBytesConnection.cs`,
    `.../Http2ProxyStreamHandler.cs`, `.../Http2ProxyTunnel.cs`, `.../HttpProxyExchange.Http2.cs`;
    `Protocols/Http/Surl.Protocol.HttpProxy.UnitTests/AlreadyReadBytesConnectionTests.cs`,
    `.../Http2ProxyStreamHandlerTests.cs`, `.../HttpProxyProtocolServerHttp2Tests.cs`,
    `.../InteractiveClientConnection.cs`, and the three fixture folders
    `.../Fixtures/https-proxy-h2-connect`, `.../Fixtures/https-proxy-h2-forward`,
    `.../Fixtures/https-proxy-h2-offered-http11-chosen` (`git checkout "stash@{N}^3" -- <folder>` takes a folder whole).
  - The fixtures were recorded with `Record-CurlExchange.ps1 -Tls -TlsAlpn h2,http/1.1` against the
    pinned static curl 8.21.0 build that carries `HTTP2` (ADR-0081; SHA-256 `589C8E4D…2648` in
    `UpstreamCurlBuilds.json`), with and without `--proxy-http2`. Do not re-record them unless a test
    shows them wrong; if you must, BL-938 is the recorder fix the relay needs.
- What remains: build, run, close the coverage gaps to 100% line and branch in
  `Surl.Protocol.HttpProxy.UnitLibrary`, keep every method at complexity 10 or less, update the library's
  `CLAUDE.md` for the HTTP/2 path.
- Stand-in: the proxy owns no shared store and adds no fault. The HTTP/2 path publishes nothing of its
  own on the instance's event stream: the engine publishes each exchange's start, bytes and end as for
  the HTTP/1.1 proxy; the notes written through `context.Log` (the ALPN `h2` note, a preface that is not
  one, a head timeout) are what a test observes. A test seeds nothing; it drives curl with
  `--proxy-http2` against an `https` proxy listener once the listener offers `h2`.

## Acceptance criteria

- [ ] `HttpProxyProtocolServerHttp2Tests` passes, including
      `Fixtures_ProxyOfferingH2ThenHttp11_CurlAgreesH2OnlyWithProxyHttp2`,
      `ServeAsync_CurlsConnectStream_Answers200AndRelaysTheTunnelInDataFrames` and
      `ServeAsync_CurlsForwardedGetStream_ForwardsItOverHttp11AndRelaysTheResponse`, each driven by the
      bytes in its fixture folder measured from pinned upstream curl 8.21.0.
- [ ] `Http2ProxyStreamHandlerTests` and `AlreadyReadBytesConnectionTests` pass, and
      `HttpProxyProtocolServerTlsTests.ServeAsync_TlsAgreedAnAlpnNeitherH2NorHttp11_ClosesWithoutWritingOrConnectingAndNotesWhy` passes.
- [ ] `dotnet build Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary -warnaserror` is clean, the fast
      tests are green, and `Surl.Protocol.HttpProxy.UnitLibrary` has 100% line and branch coverage.
- [ ] `Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary/CLAUDE.md` says a connection whose TLS agreed
      `h2` is served by `Http2ConnectionServer` with `Http2ProxyStreamHandler`.

## Notes

## Log

- 2026-11-06: Created.
