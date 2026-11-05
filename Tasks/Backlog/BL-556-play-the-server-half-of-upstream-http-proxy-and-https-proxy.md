---
id: BL-556
title: Play the server half of upstream http-proxy and https-proxy test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-553, BL-554, BL-391, BL-393]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-04
completed:
---
# BL-556 — Play the server half of upstream http-proxy and https-proxy test cases

## Goal

The upstream test-case run covers cases whose `<server>` names `http-proxy` or `https-proxy`:
Surl's HTTP proxy plays the proxy and BL-391's HTTP server half plays the origin, as BL-550's ADR
decision 8 says; the `http-proxy` and `https-proxy` rows of the server-name table in
`Surl.Conformance.UnitLibrary` say built, and the pass-rate file counts the cases.

## Context

- Decisions: ADR-0080 decisions 2 and 3 (`server-not-built:http-proxy` and
  `server-not-built:https-proxy` stop applying when the rows flip; `servercmd-unsupported:<command>`
  for any proxy instruction not implemented); BL-550's ADR decision 8 for which case parts
  (`<connect>`, `<data>`) the proxy takes. The proxy-side seam that takes a case's parts lives in
  `Surl.Protocol.HttpProxy` (planning name `HttpProxyUpstreamTestCaseReplies`, as ADR-0080 decision
  2 names the per-protocol seam).
- Cases that wait on this (tag `curl-8_21_0`, commit `68720b4837284335b2d63cb358f8f6ce65f5bc55`):
  - `http-proxy` (24): 80, 83, 95, 275, 445, 714, 715, 744, 1078, 1230, 1287, 1288, 1297, 1319,
    1320, 1321, 1428, 1849, 1904, 2050, 2055, 2107, 3028, 3106;
  - `https-proxy` (5): 1586, 1630, 1631, 1632, 3401;
  - 715 and 2055 also name `socks5`: they keep `server-not-built:socks5` until BL-562 flips that
    row, and whichever of BL-556 and BL-562 lands second runs them.
- The run, checker and pass-rate file are BL-392's and BL-393's; `%PROXYPORT` and any HTTPS-proxy
  port variable come from the server-name table.

## Acceptance criteria

- [ ] The server-name table's `http-proxy` and `https-proxy` rows say built and map their port
      variables, with a named test in `Surl.Conformance.UnitTests` pinning each row.
- [ ] Unit tests drive the proxy's case seam with inline cases: a `<connect>` reply to `CONNECT`,
      and each proxy `<servercmd>` the ADR keeps; each excluded instruction gives its named skip.
- [ ] The run covers the 27 cases above other than 715 and 2055 (and those two once `socks5` is
      built) on each reference pin; the result lines list each by number, and Notes record the
      counts per platform.
- [ ] Failing cases are grouped by cause into new tasks filed with `task-planner`, with their IDs in
      Notes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library
      keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
