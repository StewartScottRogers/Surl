---
id: BL-1718
title: Withhold API-key headers from the exchange log of WebTransport's HTTP/3 request header blocks
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1712]
touches: [Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md, Foundation/Surl.Protocol.Abstractions.UnitLibrary/HttpWithheldHeaderNames.cs, Foundation/Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
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

- [x] A fast test shows a WebTransport `HEADERS` frame carrying each name in `HttpWithheldHeaderNames.ApiKeysAndSessionTokens` logged with its payload as `****`.
- [x] A fast test shows each of those values as `****` in the decoded header text.
- [x] The redactor reads the header names from `HttpWithheldHeaderNames` and names none itself.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- `Http3RequestBytesRedactor` now takes further secret field names, as `Http2RequestBytesRedactor` does; WebTransport
  passes `ApiKeysAndSessionTokens`. The http and https servers pass none, so their HTTP/3 log is unchanged
  (ADR-0182 amendment 14).
- Tests: `WebTransportProtocolServerTests.CreateRedactor_EachApiKeyAndSessionTokenHeadersFrame_IsLoggedWithItsPayloadAsStars`
  and `..._EachDecodedApiKeyAndSessionTokenLine_IsLoggedAsStars` failed before the change and pass after it.
- Added `HttpWithheldHeaderNames.cs` and the Abstractions `CLAUDE.md` to `touches`: both said which redactors read the
  list, which this change made out of date. The only other task in `Doing` (BL-1757, NATS) names neither.
- Found by reading the code: a `HEADERS` frame split across QUIC `STREAM` frames still logs its later chunks, because
  each chunk gets a fresh redactor. Filed as BL-1761, and ADR-0182 amendment 14 states the limit.
- The help text's withheld lists are BL-1723.
- Verify: build `-warnaserror` 0 warnings, format check clean, fast tests 29491 passed, 3 skipped, 0 failed.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. WebTransport's exchange log writes API-key and session-token values as **** in HTTP/3 header blocks and decoded header text
