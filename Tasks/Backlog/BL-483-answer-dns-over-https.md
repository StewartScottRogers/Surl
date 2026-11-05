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
completed:
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

- [ ] Named tests replay RFC 8484 section 4.1.1's GET and POST examples against a zone with `www.example.com`, and
      answer `200` with `application/dns-message` and the right `Cache-Control`. They also show:
      - a bad `dns` parameter gets `400`;
      - a wrong content type gets `415`;
      - another path gets `404`;
      - the same GET answered over HTTP/2 frames.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
