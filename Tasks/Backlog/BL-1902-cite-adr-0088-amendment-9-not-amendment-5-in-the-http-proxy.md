---
id: BL-1902
title: Cite ADR-0088 Amendment 9, not Amendment 5, in the HTTP proxy's HTTP/2 doc comments
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1902 — Cite ADR-0088 Amendment 9, not Amendment 5, in the HTTP proxy's HTTP/2 doc comments

## Goal

The XML doc comments on the HTTP proxy's HTTP/2 types and tests cite the ADR-0088 amendment that decides HTTP/2 to the
HTTPS proxy.

## Context

Found during BL-1897 on 2026-11-27. Comments only. No store, event or fault changes. ADR-0088 Amendment 5 settles how
`Surl.Cli` reads the proxy options. Amendment 9 (BL-941) is the one that has the HTTPS proxy offer and serve `h2`; its
first paragraph says its number was meant to be 5. These files still cite Amendment 5:

- Library: `Http2ProxyStreamHandler.cs`, `Http2ProxyTunnel.cs`, `HttpProxyExchange.Http2.cs`.
- Tests: `Http2ProxyStreamHandlerTests.cs`, `HttpProxyProtocolServerHttp2Tests.cs`.

## Acceptance criteria

- [ ] No file in `Surl.Protocol.HttpProxy.UnitLibrary` or `Surl.Protocol.HttpProxy.UnitTests` cites ADR-0088
  Amendment 5 for HTTP/2 behaviour; each cites Amendment 9 or decision 6, as ADR-0088's text supports.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
