---
id: BL-556
title: Play the server half of upstream http-proxy and https-proxy test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-553, BL-554, BL-391, BL-393, BL-1035]
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

- 2026-11-11 (lane 1): returned to Backlog behind BL-1035. `dotnet build` cannot resolve the SDK
  `global.json` pins (10.0.401; installed are only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100), so no
  gate can be run. The first cut of the proxy seam was written and left uncommitted for the shift to stash:
  `HttpProxyUpstreamTestCaseReplies`, `HttpProxyUpstreamTestCaseReply`,
  `HttpProxyUpstreamTestCaseRecordingConnection`, `HttpProxyUpstreamTestCaseServer` and a copy of the
  HTTP half's part-number rules (`HttpProxyUpstreamTestCasePartNumber`, with its tests). None of it has
  been compiled yet.
- Findings for whoever resumes:
  - None of the 29 cases has a `<servercmd>`, so the proxy row in `UpstreamTestCaseServerCommands`
    is `Testnum` only, and any other command gives `servercmd-unsupported:<command>`.
  - Port variables: `http-proxy` is `%PROXYPORT`, `https-proxy` is `%HTTPSPROXYPORT`. Give
    `UpstreamTestCaseServer` an optional `PortVariable` to hold them.
  - `<proxy>` gets what the proxy read before a tunnel opened (test 80 lists only the `CONNECT`).
    `<protocol>` gets what the origin server half read.
  - Upstream's `sws` sends every tunnel to `127.0.0.1` at the `CONNECT`'s port, whatever host is
    named (test 80 sends `CONNECT test.80:%HTTPPORT`; test 1631 sends `CONNECT` to the FTP control
    port and then to the EPSV data port). So the conformance run's `IOriginConnector` should be
    `Surl.Networking`'s `SocketOriginConnector` (loopback only) with the host replaced by
    `127.0.0.1`. That differs from ADR-0088 decision 8's in-memory connector, which can reach only
    the HTTP half, so record it as an ADR-0088 amendment. The ADR file is not in `touches` yet, and
    no task in Doing names it.
  - Listeners: the plaintext proxy needs its own scheme because `http` is taken (for example
    `http-proxy` with `IsImplicitTls = false`). The HTTPS proxy can use `https` with
    `ListenUrl.IsProxy = true` (ALPN `http/1.1` only); the HTTP TLS half is on `wss`, so `https`
    is free.
  - Test 1230 uses `-x %HOSTIP:%HTTPPORT` and also names `http-ipv6`. The run does not compose
    `http-ipv6`, so 1230 will skip `server-half-not-composed:http-ipv6`.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: the .NET SDK 10.0.401 global.json pins is not installed, so dotnet build fails before any gate can run
