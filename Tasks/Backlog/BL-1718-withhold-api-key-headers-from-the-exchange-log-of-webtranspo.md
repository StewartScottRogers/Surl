---
id: BL-1718
title: Withhold API-key headers from the exchange log of WebTransport's HTTP/3 request header blocks
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1712]
touches: [Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1718 — Withhold API-key headers from the exchange log of WebTransport's HTTP/3 request header blocks

## Goal

WebTransport logs a request header block, and its decoded header text, with every API-key or session-token value as `****`.

## Context

- ADR-0182 decision 7 says the HTTP-carried extended servers withhold `Authorization` and API-key headers.
- `Http3RequestBytesRedactor` (`Protocols/Http/Surl.Http3.UnitLibrary`) names only `authorization` and
  `proxy-authorization`, so an `x-api-key` sent to WebTransport is logged in clear (noted in BL-1469, found again in BL-1712).
- The shared list is `HttpWithheldHeaderNames` in `Surl.Protocol.Abstractions` (BL-1712).
- The https server's own HTTP/3 redaction stays as ADR-0182 decision 7 states it unless an amendment changes it.

## Acceptance criteria

- [ ] A fast test shows a WebTransport `HEADERS` frame carrying each name in `HttpWithheldHeaderNames.ApiKeysAndSessionTokens` logged with its payload as `****`.
- [ ] A fast test shows each of those values as `****` in the decoded header text.
- [ ] The redactor reads the header names from `HttpWithheldHeaderNames` and names none itself.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
