---
id: BL-1759
title: Withhold API-key and session-token fields from the HTTP proxy's exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1717]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary/HttpWithheldHeaderNames.cs, Foundation/Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1759 — Withhold API-key and session-token fields from the HTTP proxy's exchange log

## Goal

The HTTP proxy's exchange log withholds the value of every `HttpWithheldHeaderNames.ApiKeysAndSessionTokens` field, on
HTTP/1.x and on cleartext HTTP/2.

## Context

- ADR-0182 amendment 13 (BL-1717) accepted this limit: an origin reached in cleartext through the proxy has these fields
  logged in clear in the tunnel's log, though the origin's own log withholds them for gRPC, DNS over HTTPS and the other
  HTTP-carried extended servers. This task closes the limit for the proxy; the SOCKS tunnel is a separate task.
- `HttpProxyConnectionBytesRedactor` (`Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary`) makes both
  `HttpRequestHeadBytesRedactor` and `Http2RequestBytesRedactor` with no further names. Both constructors take them.
- The proxy cannot tell which origin it carries, so it withholds the fields in every request it redacts, forwarded
  requests and `CONNECT` tunnels alike. With `--proxy` named, origin `http` and `https` listen URLs use this redactor
  too (amendment 8, point 1), so their logs withhold the fields as well. That withholds more, never less.
- The amendment replaces the HTTP proxy's part of amendment 13, point 3 and of its accepted limit. Update the doc
  comment of `HttpWithheldHeaderNames` and the abstractions library's `CLAUDE.md` where they say who withholds the list.
- HTTP/1.x writes the whole value as `****`; HTTP/2 withholds the header block's payloads (amendment 3's form).
- If `AiHelpProse` or `ManualText` (`Application/Surl.Cli.UnitLibrary`) say the proxy does not withhold these fields when
  this task runs, file a follow-up; help text is not part of this task (BL-1723 covers the withheld lists).
- Stand-in: only the levelled log and trace dumps change. The event stream keeps raw bytes (ADR-0182 decision 4), so a
  test still sees what the client sent; no store or fault is involved.

## Acceptance criteria

- [ ] `HttpProxyExchangeLogRedactionTests.Http1RequestThroughTheProxy_ApiKeyFields_AreLoggedAsMasks` passes for each name
      in `HttpWithheldHeaderNames.ApiKeysAndSessionTokens`, in a forwarded request and in a `CONNECT` tunnel.
- [ ] `HttpProxyExchangeLogRedactionTests.Http2RequestThroughTheProxy_ApiKeyFields_WithholdTheHeaderBlock` passes for
      each of those names on prior-knowledge HTTP/2 in a `CONNECT` tunnel: each `HEADERS` payload is `****`.
- [ ] The proxy library reads the field names from `HttpWithheldHeaderNames` and names none of the five itself.
- [ ] A new ADR-0182 amendment (the next free number) states the above, and the redactor's doc comment, the
      `HttpWithheldHeaderNames` doc comment and both libraries' `CLAUDE.md` files agree with it.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the proxy library stays at 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
