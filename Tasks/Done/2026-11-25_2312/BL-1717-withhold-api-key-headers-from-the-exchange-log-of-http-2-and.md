---
id: BL-1717
title: Withhold API-key headers from the exchange log of HTTP/2 and gRPC request header blocks
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1712]
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md, Foundation/Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md, Foundation/Surl.Protocol.Abstractions.UnitLibrary/HttpWithheldHeaderNames.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1717 — Withhold API-key headers from the exchange log of HTTP/2 and gRPC request header blocks

## Goal

gRPC and DNS over HTTPS on HTTP/2 log a request header block that carries an API-key or session-token header with its payload as `****`.

## Context

- ADR-0182 decision 7 says the HTTP-carried extended servers withhold `Authorization` and API-key headers.
- `Http2RequestBytesRedactor.IsSecretName` (`Protocols/Http/Surl.Http2.UnitLibrary`) names only `authorization` and
  `proxy-authorization`, so an `x-api-key` sent to gRPC is logged in clear (noted in BL-1469, found again in BL-1712).
- The shared list is `HttpWithheldHeaderNames` in `Surl.Protocol.Abstractions` (BL-1712).
- The http and https servers' own HTTP/2 redaction stays as ADR-0182 decision 7 states it unless an amendment changes it.

## Acceptance criteria

- [x] A fast test shows a gRPC header block carrying each name in `HttpWithheldHeaderNames.ApiKeysAndSessionTokens` logged with its payload as `****`.
- [x] A fast test shows the same for DNS over HTTPS on HTTP/2.
- [x] The redactor reads the header names from `HttpWithheldHeaderNames` and names none itself.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Both new server tests failed before the fix (the block was logged as received):
  `GrpcProtocolServerTests.CreateRedactor_EachApiKeyAndSessionTokenHeaderBlock_IsLoggedWithItsPayloadAsStars` and
  `DnsConnectionBytesRedactorTests.Redact_Http2RequestWithEachApiKeyAndSessionTokenHeader_LogsItsPayloadAsStars`.
- Decision (ADR-0182 amendment 13): `Http2RequestBytesRedactor` always withholds `Credentials` and takes further
  names from its caller; only gRPC and DNS over HTTPS pass `ApiKeysAndSessionTokens`. The http and https servers,
  the HTTP proxy and the SOCKS tunnel are unchanged, as the Context requires for http and https.
- Added to `touches`: `Surl.Protocol.Abstractions.UnitLibrary`'s `CLAUDE.md` and `HttpWithheldHeaderNames.cs`,
  whose text became false (documentation only). No task in Doing names that project.
- Fast tests: 29,468 passed, 3 skipped, 0 failed. Build: 0 warnings. Format check clean.
- Filed BL-1758 (HTTP/1.1 sent to a gRPC listen URL is logged in clear), BL-1759 and BL-1760 (API keys of
  tunnelled requests in the HTTP proxy's and SOCKS logs).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. gRPC and DNS over HTTPS on HTTP/2 log a header block carrying an API-key or session-token field with its payload as ****
