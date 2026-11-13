---
id: BL-486
title: Run the Curl port's dns client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1032, BL-1017, BL-1018, BL-1019, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/DnsQuery.md]
requirement: none
created: 2026-11-04
completed: 2026-11-12
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

- [x] The test class exists with the four queries over each transport, each passing on Windows with the pinned
      client present.
- [x] `Documentation/Conformance/Extended/DnsQuery.md`: every row is covered by a named test or marked `n/a` with
      a reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- `CurlPortClientQueriesSurlOverDnsTests`: A, AAAA, MX and a missing name over udp, tcp, tls, https and quic, 20 cases, all passing on Windows with the pinned client. One surl per transport (`--dns-transport <t>`, port 0) over a zone file under `.surl/dns`. Output is asserted as dig-style text lines, which is the port's rendering; the values asserted are the zone's. No port defect found.
- Rows left naming their server tasks are proven by those tasks' unit tests on bytes; the live client cannot observe them (padding, ALPN, flags beyond AA, EDNS).

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-485 replaced by BL-1032, BL-1017, BL-1018, BL-1019 (BL-485 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. The Curl port's dns client queries a live surl for A, AAAA, MX and a missing name over udp, tcp, tls, https and quic; DnsQuery.md rows link the tests
