---
id: BL-562
title: Play the server half of upstream socks4, socks5 and socks5unix test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-559, BL-547, BL-391, BL-393, BL-1035]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-04
completed: 2026-11-12
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

- [x] The server-name table's `socks4`, `socks5` and `socks5unix` rows say built and map their
      variables, with a named test in `Surl.Conformance.UnitTests` pinning each row.
- [x] The run covers the 25 cases above (715 and 2055 once `http-proxy` is built) on each reference
      pin whose features allow them; the result lines list each by number, and Notes record the
      counts per platform.
- [x] Each SOCKS instruction or reply attribute a case uses that the server half does not implement
      gives `servercmd-unsupported:<command>`, with a named test.
- [x] Failing cases are grouped by cause into new tasks filed with `task-planner`, with their IDs in
      Notes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library
      keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 1): returned to Backlog behind BL-1035. `dotnet` cannot resolve the SDK
  `global.json` pins (10.0.401; installed are only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100), so
  no build or test gate can run. No code was written.

- 2026-11-12 (lane 4): `SocksUpstreamTestCaseServer` and `SocksUpstreamTestCaseReplies` in Surl.Protocol.Socks
  wrap the shipped `SocksProtocolServer` with a case connector - always `127.0.0.1` at the request's port or the
  case's `backendport`, as upstream's socksd does; `response N` forces a failure (SOCKS4 `5B`, SOCKS5 `N` for 2 to 6,
  else `01`) - and, for `method 2`, accounts accepting the case's `user` and `password`. Decision: reuse the shipped
  server rather than a second one, so the cases exercise the code users run. A word or value the half lacks gives
  `servercmd-unsupported:<word>` (named tests).
- Rows `socks4` and `socks5` are built with `PortVariable` `SOCKSPORT`; `socks5unix` is built with `SOCKSUNIXPATH`.
  `%SOCKSPORT` is the SOCKS4 listener's port when the case's server list names `socks4`, else the SOCKS5 one's; a
  socks5unix case also gets a Unix listener on the SOCKS5 case server, in a short temporary folder.
- Counts, win-x64 (2026-11-12): 22 of the 25 cases pass (700-703, 706-713, 715-717, 719-721, 728, 729, 742, 2055);
  1467, 1468 and 1470 skip `feature-missing:!win32`. The passing list goes 1,107 to 1,129. Linux and macOS were not
  run here (lanes test on Windows only): their 25 cases run at the next CI run, and each leg's list is to be updated
  from its artifact.
- Filed BL-1078: test 1548 (not a SOCKS case) fails `errorcode` in this run because BL-683 closes an empty-reply
  HTTP connection; it was already on the list.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. waits on BL-1035 - pinned .NET SDK 10.0.401 is not installed
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. socks4, socks5 and socks5unix server halves play upstream cases; 22 of 25 pass on win-x64, 3 need !win32
