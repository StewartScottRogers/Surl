---
id: BL-483
title: Answer DNS over HTTPS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-482, BL-407, BL-409]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-483 — Answer DNS over HTTPS

## Goal

The DNS server answers RFC 8484 DNS over HTTPS on the ADR's path: `GET` with `?dns=` (base64url, no padding) and
`POST` with `application/dns-message`. It answers over HTTP/1.1 and HTTP/2, with `Cache-Control: max-age` taken
from the answer's smallest TTL.

## Context

- Decision: BL-478's ADR, decisions 5 and 6.
- HTTP/1.1: BL-407's library. HTTP/2: BL-409's library (add both references, which their ADR-0002
  rows allow).
- RFC 8484:
  - section 4.1 (the query; the DNS ID should be 0);
  - section 4.1.1 (the `www.example.com` GET and POST examples, which are the tests' bytes);
  - section 4.2 (`application/dns-message`, and `415` for another type);
  - section 5.1 (freshness from the smallest TTL);
  - section 6 (the media type).
  RFC 4648 section 5 for base64url.

## Acceptance criteria

- [x] Named tests replay RFC 8484 section 4.1.1's GET and POST examples against a zone with `www.example.com`, and
      answer `200` with `application/dns-message` and the right `Cache-Control`. They also show:
      - a bad `dns` parameter gets `400`;
      - a wrong content type gets `415`;
      - another path gets `404`;
      - the same GET answered over HTTP/2 frames.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

What was built (2026-11-05), all in `Surl.Extended.Protocol.DnsQuery` (no `touches` change):

- `DnsOverHttpsHandler` is both `IHttpRequestHandler` (HTTP/1.1 through `Http1ConnectionServer`) and
  `IHttp2StreamHandler` (HTTP/2 through `Http2ConnectionServer`): one `AnswerAsync` decides both, as ADR-0092
  decision 5 says. HTTP/2 hands it the body through `DnsHttp2RequestBody` (an `IHttpRequestBody` over
  `Http2RequestStream`) and resets `PROTOCOL_ERROR` on a content-length mismatch, as the HTTP server does.
- `DnsOverHttpsQueryParameter` (the one `dns` parameter, RFC 4648 section 5 alphabet only), `DnsOverHttpsQueryBuffer`
  (the 65535-byte `POST` bound, `413`), `DnsOverHttpsCacheLifetime` (`max-age`), and `DnsQueryAnswerer.AnswerOverHttp`
  (allowed AXFR and IXFR are `NOTIMP`, ADR-0092 decision 4).
- The library now references `Surl.HttpServing` and `Surl.Http2`, horizontal libraries in ADR-0002's table as
  amended; `ProtocolIsolationTests` passes.
- Tests: `DnsOverHttpsHandlerTests` replays RFC 8484 section 4.1.1's three examples - the `www.example.com` GET and
  POST answer `200`, `application/dns-message`, `Cache-Control: max-age=128` from the A record's TTL; the
  62-character-label GET answers NXDOMAIN with `max-age=300`, the SOA's negative TTL - plus `400` for every bad `dns`
  value and for a dropped query, `415`, `404`, `405` with `Allow`, `413` (announced, chunked past 65535, past the
  upload limit), keep-alive, and the GET and POST over HTTP/2 frames (`DnsOverHttpsHandlerTests.Http2.cs`, frames
  written by hand from RFC 9113 in `DnsHttp2TestFrames`, since `Surl.Http2`'s codec is internal).
- Results: `Surl.Extended.Protocol.DnsQuery.UnitTests` 323 passed; every fast test project green;
  `dotnet build -warnaserror` clean; `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.DnsQuery.UnitLibrary`
  100% line, 100% branch, 0 failing members (worst CRAP 10).
- Also: `DnsQueryAnswerer.AnswerWith` (16) and `OptOf` (14) were already past the complexity 10 the audit measures;
  split into `AnswerQueryHeader`, `EchoedQuestions`, `OptRecordsOf` and two predicates, with no behaviour change.

Choices made (sensible defaults inside ADR-0092 decision 5, decided by Claude under Stewart's delegation):

- `max-age` for a response with no answer and no SOA (an error, `REFUSED`, or a referral) is 0: the ADR names the
  answer section and the negative answer's SOA only, and a referral is no answer a DoH client should cache.
- `HEAD` is `405` like every method but GET and POST: the ADR's `Allow: GET, POST` names exactly those.
- The `dns` value is not percent-decoded: base64url needs no escaping, so `%` (like `=`, `+` or whitespace) is
  outside the alphabet and `400`.
- Every answer carries `Date` and `Server: surl`, as the other HTTP-based extended servers do (RFC 9110 section 6.6.1).
- Left to tasks filed here: EDNS(0) padding of DoH answers (BL-783) and the optional DoH login (BL-784). Binding the
  DoH listener with TLS and ALPN `h2`/`http/1.1` is BL-485's.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The DNS server answers RFC 8484 DoH on /dns-query: GET ?dns= and POST application/dns-message over HTTP/1.1 and HTTP/2, with Cache-Control max-age from the smallest TTL
