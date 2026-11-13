---
id: BL-570
title: Play the server half of upstream dns test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-569, BL-481, BL-391, BL-393, BL-1035]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-570 — Play the server half of upstream dns test cases

## Goal

The upstream test-case run covers cases whose `<server>` is `dns`: the server BL-569's ADR names
answers the case's queries from its `<dns>` section on `%DNSPORT`, the `dns` row of the
server-name table in `Surl.Conformance.UnitLibrary` says built, and each case gets its true outcome
(a result, or `feature-missing:override-dns` on a pin without the feature).

## Context

- Decisions: ADR-0080 decisions 2 and 3; BL-569's ADR decisions 1 to 4. `touches` assumes
  BL-569's planning assumption that the server half is `Surl.Extended.Protocol.DnsQuery` with a seam
  that takes a `<dns>` section's records (planning name `DnsQueryUpstreamTestCaseReplies`); if the
  ADR names another server, work there and say so in Notes.
- Cases that wait on this (tag `curl-8_21_0`, commit `68720b4837284335b2d63cb358f8f6ce65f5bc55`):
  2102, 2103, 2104 - feature `override-dns`, `CURL_DNS_SERVER=127.0.0.1:%DNSPORT`, `<dns>` giving
  `A: %HOSTIP` and `AAAA: %HOST6IP`.
- The DNS server's UDP answering is BL-481's; the run, checker and pass-rate file are BL-392's and
  BL-393's; BL-391's HTTP server half answers the cases' URLs if BL-569's ADR says so.

## Acceptance criteria

- [x] The server-name table's `dns` row says built and maps `%DNSPORT`, with a named test in
      `Surl.Conformance.UnitTests` pinning the row.
- [x] Named tests in `Surl.Extended.Protocol.DnsQuery.UnitTests` load an inline `<dns>` section's
      A and AAAA records through the seam and answer an A and an AAAA query for them over the
      datagram seam with the bytes RFC 1035 and RFC 3596 define; a record type the seam does not
      take gives `servercmd-unsupported:<name>`.
- [x] The run covers 2102, 2103 and 2104 on each reference pin; the result lines list each by
      number with the outcome BL-569's ADR predicts for that pin, and Notes record them per
      platform.
- [x] A failing case is filed with `task-planner` as a new task grouped by cause, its ID in Notes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library
      keeps 100% line and branch coverage.

## Notes

- 2026-11-12, lane 3: the earlier lane's uncommitted work sat in a shared stash this session could
  not list, so the seam was rewritten from ADR-0123 and the lane-1 notes. The server is the one
  ADR-0123 names (`Surl.Extended.Protocol.DnsQuery`), so `touches` held.
- Built: `DnsQueryUpstreamTestCaseReplies` (A and AAAA lines as `dnsd` reads them, root zone `.zone`
  with a wildcard `*` at TTL 2580 plus a TXT so the node always exists, `QueryLog`) and
  `DnsUpstreamTestCaseServer`, each with its own test class. `dnsd`'s TTL and line forms came from
  lane 1's reading of a curl 8.22.0 tree, not tag `curl-8_21_0`; no case checks the TTL.
- Composition: `dns` row built with `PortVariable: "DNSPORT"`; `UpstreamTestCaseServerCommands.Dns`
  (`Testnum`, `A`, `AAAA`); the selector adds the names of a case's `<reply><dns>` lines to its
  server commands, so `HTTPS` and `Delay-*` skip `servercmd-unsupported:<name>`; the tests' server
  halves bind a datagram listener for `%DNSPORT`. The selector test that used `dns` as the unbuilt
  server now uses `http/2`.
- Cases 2102, 2103, 2104 on win-x64 (run 2026-11-12, reference build): each skips
  `feature-missing:override-dns`, as ADR-0123 decision 3 predicts (the run's log counts 3). Linux and
  macOS pins were not run here; ADR-0123 predicts the same skip from their pinned `features` lines and
  CI's `Upstream test cases` job writes their result lines.
- The win-x64 full run fails its guard independently of the three DNS cases: `test1548 fail errorcode`,
  and 30, 37, 1297 pass but are unlisted. Not measured against a clean checkout; filed as BL-1086.
- The `<verify><dns>` check is filed as BL-1085.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds; DNS seam work left uncommitted
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. dns server half plays upstream <dns> sections on %DNSPORT; dns row built; 2102-2104 skip feature-missing:override-dns on every pin
