---
id: BL-569
title: Decide what plays the server half of upstream dns test cases
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-478]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed:
---
# BL-569 — Decide what plays the server half of upstream dns test cases

## Goal

An ADR decides what answers the DNS queries of upstream test cases whose `<server>` is `dns`, and
records, for each reference pin, whether it has the `override-dns` feature those cases require.

## Context

- ADR-0080 decision 2 marks `dns` `server-not-built`: "the test DNS server `<dns>` sections talk
  to". Cases that wait on it (tag `curl-8_21_0`, commit `68720b4837284335b2d63cb358f8f6ce65f5bc55`,
  classified with ADR-0080's rules by BL-399): 2102, 2103, 2104 - feature `override-dns`,
  environment `CURL_DNS_SERVER=127.0.0.1:%DNSPORT`, and a `<dns>` section giving `A: %HOSTIP` and
  `AAAA: %HOST6IP`.
- The candidate: the extended DNS server BL-478 to BL-482 build,
  `Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary`, answering A and AAAA over UDP
  (BL-481) from zones (BL-480). ADR-0083 governs the extended servers: judged by their protocol's
  specification, and `SURL_EXTENSIONS=0` turns them off in `surl`; upstream curl as a client of one
  is allowed (upstream curl is the oracle everywhere). The alternative is a smaller server elsewhere;
  `Surl.Conformance.UnitLibrary` references Abstractions only (ADR-0080 decision 2).
- `override-dns` may be a feature only debug builds report, in which case every reference pin (all
  release builds, ADR-0080 decision 3) skips the three cases `feature-missing:override-dns` and the
  server half is never exercised by them. Measure it: read each reference pin's `curl -V` features
  line, and whether `CURL_DNS_SERVER` changes where a pinned release build sends its queries, with
  `Record-CurlExchange.ps1` (extended for a UDP listener if it lacks one, rather than a throwaway
  server). State the curl version and pin checked.

Decide and record:

1. What plays the `dns` server half (planning assumption: `Surl.Extended.Protocol.DnsQuery`'s UDP
   server, composed by `Surl.Conformance.UnitTests` with a zone built from the case's `<dns>`
   section), and how a `<dns>` section becomes its zone (planning assumption: a seam in the DNS
   library taking the section's records, as ADR-0080 decision 2's per-protocol seams do).
2. Whether `SURL_EXTENSIONS=0` affects the conformance run (planning assumption: no, the run
   composes the library directly).
3. `override-dns` per reference pin (Windows, Linux, macOS), measured, and so whether 2102, 2103
   and 2104 run anywhere at the reference release; if none, whether the row still flips (planning
   assumption: yes, so the skip reason is `feature-missing:override-dns`, which is true, rather than
   `server-not-built:dns`).
4. Which port variable `%DNSPORT` maps to, and whether the cases' URLs are answered by BL-391's
   HTTP server half.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked
      "Decided by Claude under Stewart's delegation", answering decisions 1 to 4, and it is listed in
      the folder's `README.md`.
- [ ] The ADR states, per reference pin by its file and SHA-256 from `UpstreamCurlBuilds.json`,
      whether `override-dns` is present and how that was measured, and names cases 2102, 2103 and
      2104 with the outcome each will have on each pin.
- [ ] The ADR's "Consequences" name BL-570 as the task that acts on it.

## Notes

- Planning assumptions above are BL-399's defaults for an unattended plan; the ADR may change any
  of them.

## Log

- 2026-11-04: Created.
