---
id: BL-562
title: Play the server half of upstream socks4, socks5 and socks5unix test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-559, BL-547, BL-391, BL-393]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-04
completed:
---
# BL-562 — Play the server half of upstream socks4, socks5 and socks5unix test cases

## Goal

The upstream test-case run covers cases whose `<server>` names `socks4`, `socks5` or
`socks5unix`: Surl's SOCKS server plays the proxy (on BL-547's Unix listener at `%SOCKSUNIXPATH`
for `socks5unix`) in front of BL-391's HTTP server half, as BL-557's ADR decision 7 says; the
three rows of the server-name table in `Surl.Conformance.UnitLibrary` say built, and the pass-rate
file counts the cases.

## Context

- Decisions: ADR-0080 decisions 2 and 3 (`server-not-built:socks4`, `:socks5`, `:socks5unix` stop
  applying when the rows flip); BL-557's ADR decision 7; BL-571's ADR for the socket path. If the
  SOCKS server needs a seam to take a case's parts, it lives in `Surl.Protocol.Socks` (planning name
  `SocksUpstreamTestCaseReplies`); if it needs none, Notes say so.
- Cases that wait on this (tag `curl-8_21_0`, commit `68720b4837284335b2d63cb358f8f6ce65f5bc55`):
  - `socks4` (5): 700, 702, 706, 708, 729;
  - `socks5` (17): 701, 703, 707, 709, 710, 711, 712, 713, 715, 716, 717, 719, 720, 721, 728, 742,
    2055;
  - `socks5unix` (3): 1467, 1468, 1470 - features `proxy`, `UnixSockets`, `!win32`, so they run on
    the Linux and macOS reference pins only and skip `feature-missing` on Windows;
  - 715 and 2055 also name `http-proxy`: they keep `server-not-built:http-proxy` until BL-556 flips
    that row, and whichever of BL-556 and BL-562 lands second runs them.
- The run, checker and pass-rate file are BL-392's and BL-393's; `%SOCKSPORT` and `%SOCKSUNIXPATH`
  come from the server-name table.

## Acceptance criteria

- [ ] The server-name table's `socks4`, `socks5` and `socks5unix` rows say built and map their
      variables, with a named test in `Surl.Conformance.UnitTests` pinning each row.
- [ ] The run covers the 25 cases above (715 and 2055 once `http-proxy` is built) on each reference
      pin whose features allow them; the result lines list each by number, and Notes record the
      counts per platform.
- [ ] Each SOCKS instruction or reply attribute a case uses that the server half does not implement
      gives `servercmd-unsupported:<command>`, with a named test.
- [ ] Failing cases are grouped by cause into new tasks filed with `task-planner`, with their IDs in
      Notes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library
      keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
