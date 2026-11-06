---
id: BL-486
title: Run the Curl port's dns client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-485, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/DnsQuery.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-486 — Run the Curl port's dns client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl dns://` over UDP, TCP, TLS, HTTPS and
QUIC. They query an A, an AAAA, an MX and a missing name. Each assertion comes from the RFCs and the zone given,
and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help dns` and its cases in `Curl.Conformance.Extended.UnitTests/DnsQuery/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [ ] The test class exists with the four queries over each transport, each passing on Windows with the pinned
      client present.
- [ ] `Documentation/Conformance/Extended/DnsQuery.md`: every row is covered by a named test or marked `n/a` with
      a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
