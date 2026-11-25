---
id: BL-1713
title: Withhold HTTP/2 header credentials from the HTTP proxy's exchange log, tunnelled or not
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1713 — Withhold HTTP/2 header credentials from the HTTP proxy's exchange log, tunnelled or not

## Goal

The HTTP and HTTPS proxy's levelled log never holds an `authorization` or `proxy-authorization` value carried in HTTP/2, either on curl's own connection to the proxy (`--proxy-http2`) or inside a `CONNECT` tunnel.

## Context

- Found while working BL-1709 (the same gap in the SOCKS tunnel, ADR-0182 Amendment 7), by reading the code; not yet
  reproduced by a test.
- `HttpProxyProtocolServer.CreateRedactor` always returns `HttpRequestHeadBytesRedactor`, which judges header lines.
  HTTP/2 carries the credential as an HPACK field, so neither the HTTPS proxy's `h2` connection
  (`HttpProxyExchange.Http2.cs`) nor HTTP/2 cleartext inside a `CONNECT` tunnel is masked.
- The library already references `Surl.Http2`, whose public `Http2RequestBytesRedactor` serves the connection form.
  `Surl.Protocol.Http`'s `HttpConnectionBytesRedactor` and SOCKS's `SocksTextTunnelBytesRedactor` show the switching.
- An HTTP/2 proxy stream can itself tunnel (`Http2ProxyTunnel`): its tunnelled bytes travel in `DATA` frames, which
  the HTTP/2 redactor passes through; check whether credentials inside them need withholding too.

## Acceptance criteria

- [ ] A named fast test in `Surl.Protocol.HttpProxy.UnitTests` shows an HPACK `proxy-authorization` on an `h2` proxy
      connection absent from the redacted output.
- [ ] A named test shows an HPACK `authorization` sent after the HTTP/2 preface inside a `CONNECT` tunnel absent from
      the redacted output.
- [ ] ADR-0182 gains an amendment saying what is logged instead.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the proxy library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
