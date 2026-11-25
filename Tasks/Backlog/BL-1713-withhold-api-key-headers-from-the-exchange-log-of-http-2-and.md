---
id: BL-1713
title: Withhold API-key headers from the exchange log of HTTP/2 and gRPC request header blocks
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1712]
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1713 — Withhold API-key headers from the exchange log of HTTP/2 and gRPC request header blocks

## Goal

gRPC and DNS over HTTPS on HTTP/2 log a request header block that carries an API-key or session-token header with its payload as `****`.

## Context

- ADR-0182 decision 7 says the HTTP-carried extended servers withhold `Authorization` and API-key headers.
- `Http2RequestBytesRedactor.IsSecretName` (`Protocols/Http/Surl.Http2.UnitLibrary`) names only `authorization` and
  `proxy-authorization`, so an `x-api-key` sent to gRPC is logged in clear (noted in BL-1469, found again in BL-1712).
- The shared list is `HttpWithheldHeaderNames` in `Surl.Protocol.Abstractions` (BL-1712).
- The http and https servers' own HTTP/2 redaction stays as ADR-0182 decision 7 states it unless an amendment changes it.

## Acceptance criteria

- [ ] A fast test shows a gRPC header block carrying each name in `HttpWithheldHeaderNames.ApiKeysAndSessionTokens` logged with its payload as `****`.
- [ ] A fast test shows the same for DNS over HTTPS on HTTP/2.
- [ ] The redactor reads the header names from `HttpWithheldHeaderNames` and names none itself.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
