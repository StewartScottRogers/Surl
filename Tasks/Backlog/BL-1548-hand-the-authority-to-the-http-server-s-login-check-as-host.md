---
id: BL-1548
title: Hand the :authority to the HTTP server's login check as host on HTTP/2 and HTTP/3
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1547]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1548 — Hand the :authority to the HTTP server's login check as host on HTTP/2 and HTTP/3

## Goal

A request over HTTP/2 or HTTP/3 that pinned upstream curl 8.21.0 signed with `--aws-sigv4` is accepted by the
HTTP server, because the login check sees a `host` field built from `:authority`, as BL-1547's ADR-0043
amendment decides.

## Context

- Cause, in today's code: `HttpRequestAnswerer.AuthenticationRequest`
  (`Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpRequestAnswerer.cs:208-212`) builds the
  `HttpAuthenticationRequest` from `request.Fields` only. Over HTTP/2 and HTTP/3 those fields never hold
  `host`: `Http2RequestHeadMapping.Map` (`Protocols/Http/Surl.Http2.UnitLibrary/Http2RequestHeadMapping.cs:49-52`)
  keeps pseudo-headers out of `Fields` and puts `:authority` on `Authority` (lines 133-143), and
  `Http2StreamAnswerWriter.cs:53` / `Http3StreamAnswerWriter.cs:49` pass it on as `HttpRequest.Authority`.
  `AwsSigV4CanonicalRequest.BuildHeaders`
  (`Foundation/Surl.Authentication.UnitLibrary/AwsSigV4CanonicalRequest.cs:90-109`) then finds no field for
  the signed `host` and returns `null`, so every such login is refused: 401 by default, 403 under
  `--auth aws-sigv4`.
- Upstream curl 8.21.0 signs `host:<URL authority>` and sends it as `:authority` with no `Host` field
  (`lib/http_aws_sigv4.c` `make_headers`; `lib/http.c` `Curl_http_req_to_h2` and `h2_permissible_field`).
  The canonical request is therefore byte for byte the one curl signs over HTTP/1.1, so the HTTP/1.1
  fixture `Foundation/Surl.Authentication.UnitTests/Fixtures/aws-sigv4-get/request.bin`
  (`Host: 127.0.0.1:18122`, measured from pinned upstream curl) supplies the signed fields a test needs; no
  new measurement is needed for the unit tests. BL-1549 proves it with pinned curl end to end.
- The change: in `AuthenticationRequest`, when `request.Authority` is not `null` and `request.Fields` holds
  no field named `host` (case-insensitively), hand the session the fields with `("host", Authority)` added;
  otherwise the fields as received. `HttpRequest.Fields` itself, the exchange log and every other use of the
  fields stay unchanged. HTTP/1.1 requests always carry `Host`, so they are judged exactly as today.
- Tests go in `Protocols/Http/Surl.Protocol.Http.UnitTests/AuthenticationTests.cs` (or a new
  `AuthenticationTests.Authority.cs` partial), using `UnitTestAuthenticationPolicy.JudgedRequests` to see
  what the session was handed, and `Http2StreamAnswerWriterTests.cs` for one request through the HTTP/2 path.
- Stand-in: no store, event or fault changes; `LoginJudgedEvent`s are published as today, now with
  `is_accepted` true for these logins. Out of scope: the extended servers that build their own
  `HttpAuthenticationRequest` (gRPC, WebTransport, SignalR, MCP); they get their own task if needed.

## Acceptance criteria

- [ ] A test shows an HTTP/2 request with `Authority` `127.0.0.1:18122` and no `host` field hands the
  session a `host: 127.0.0.1:18122` field, after the fields as received.
- [ ] A test shows a request that already carries `host` (HTTP/1.1, or HTTP/2 with a `host` field) hands the
  session its fields unchanged, with no second `host`.
- [ ] A test shows a request with `Authority` `null` and no `host` hands the fields unchanged.
- [ ] A test drives one HTTP/2 stream through `Http2StreamAnswerWriter` whose request carries the
  `aws-sigv4-get` fixture's `Authorization`, `x-amz-date` and `x-amz-content-sha256` and `:authority`
  `127.0.0.1:18122`, and shows the judged request's fields include that `host`.
- [ ] `dotnet build Protocols/Http/Surl.Protocol.Http.UnitLibrary -warnaserror` is clean and
  `dotnet test Protocols/Http/Surl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes,
  with `HttpRequestAnswerer` at 100% line and branch coverage.
- [ ] `Protocols/Http/Surl.Protocol.Http.UnitLibrary/CLAUDE.md` says the login check sees `host` from
  `:authority` on HTTP/2 and HTTP/3 (ADR-0043 amendment, BL-1548).

## Notes

## Log

- 2026-11-21: Created.
