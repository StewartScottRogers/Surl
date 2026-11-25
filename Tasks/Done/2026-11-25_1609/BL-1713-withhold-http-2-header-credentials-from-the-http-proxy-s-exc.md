---
id: BL-1713
title: Withhold HTTP/2 header credentials from the HTTP proxy's exchange log, tunnelled or not
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
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

- [x] A named fast test in `Surl.Protocol.HttpProxy.UnitTests` shows an HPACK `proxy-authorization` on an `h2` proxy
      connection absent from the redacted output.
- [x] A named test shows an HPACK `authorization` sent after the HTTP/2 preface inside a `CONNECT` tunnel absent from
      the redacted output.
- [x] ADR-0182 gains an amendment saying what is logged instead.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the proxy library stays at 100% line and branch
      coverage.

## Notes

- Plan (`protocol-architect`, 2026-11-25). Composed `surl` never used the proxy's redactor:
  `ProxyRoutingProtocolServer.CreateRedactor` preferred the origin HTTP server's, so a proxy-only fix would have left
  the tunnel leak in the product. The router now prefers the proxy's redactor, which for a conforming client masks what the origin's does (ADR-0182
  Amendment 8 lists the exceptions).
- `touches` gained `Application/Surl.Composition.UnitLibrary` and `.UnitTests` for that router change; no task in
  `Doing` named them.
- A new `HttpProxyConnectionBytesRedactor` switches to `Http2RequestBytesRedactor` when the client preface is where a
  request may start, and withholds every later byte when it appears anywhere else (fail closed, as ADR-0182
  Amendment 7 does for SOCKS).
- `DATA` frames on an HTTP/2 `CONNECT` stream do carry credentials (`https-proxy-h2-connect` shows plaintext HTTP/1.1
  in them) and are logged as received. Fixing that changes `Surl.Http2`, which BL-1714 (claimed by lane 2) holds, so
  it is filed as BL-1733 (High) and not done here: **a credential tunnelled that way can still be logged**.
  BL-1714 finished on the shift branch during this run; this lane may not rebase, so BL-1733 builds on it.
- Follow-ups filed: BL-1733 (`DATA` frames), BL-1734 (FTP and mail logins in a `CONNECT` tunnel), BL-1735 (help text),
  BL-1736 (request start after `Content-Length: 0`). The shift branch already uses these numbers, so integration's
  `dedupe` renumbers them.
- Evidence: criteria 1 and 2 are `H2ProxyConnection_HpackProxyAuthorization_IsAbsentFromTheLog` and
  `ConnectTunnel_HpackAuthorizationAfterThePreface_IsAbsentFromTheLog`. `dotnet build -warnaserror` clean, format
  clean, fast tests 29132 passed, 0 failed, 3 skipped; `Measure-CodeQuality.ps1`: proxy and composition libraries
  100% line and branch, 0 failing members. `code-reviewer`: no new leak; its must-fix (the amendment) is done.
- Conformance stage skipped: only surl's own log changes, no byte curl sends or receives.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The HTTP proxy's exchange log withholds HTTP/2 authorization and proxy-authorization header blocks on h2 and CONNECT-tunnel connections, in composed surl too; h2 CONNECT DATA frames are filed as BL-1733
