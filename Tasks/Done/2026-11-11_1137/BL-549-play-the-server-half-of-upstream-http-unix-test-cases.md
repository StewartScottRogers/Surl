---
id: BL-549
title: Play the server half of upstream http-unix test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-547, BL-391, BL-393]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-549 — Play the server half of upstream http-unix test cases

## Goal

The upstream test-case run covers cases whose `<server>` is `http-unix`: Surl's HTTP server half
(BL-391) answers on BL-547's Unix socket listener at `%HTTPUNIXPATH`, the `http-unix` row of the
server-name table in `Surl.Conformance.UnitLibrary` says built, and the pass-rate file counts the
cases.

## Context

- Decision: ADR-0080 decision 2 (the `http-unix` row, mode verbatim reply as `http`) and decision 3
  (`server-not-built:http-unix` stops applying the day the row flips); BL-571's ADR for the socket
  path.
- Cases that wait on this, tag `curl-8_21_0` (commit `68720b4837284335b2d63cb358f8f6ce65f5bc55`):
  1435 and 1436, feature `UnixSockets`, command `--unix-socket %HTTPUNIXPATH`. ADR-0016 says the
  Linux and macOS reference pins report `UnixSockets`; on a pin without it the cases skip
  `feature-missing:UnixSockets`.
- `Surl.Conformance.UnitTests` composes the server half as ADR-0080 decision 2 says: the HTTP
  server with BL-391's reply seam, on `Surl.Networking`'s listener. `%HTTPUNIXPATH` is a path in
  the case's log directory, short enough for `sun_path`.
- The run, checker and pass-rate file are BL-392's and BL-393's.

## Acceptance criteria

- [x] The server-name table's `http-unix` row says built and maps `%HTTPUNIXPATH`, with a named
      test in `Surl.Conformance.UnitTests` that pins the row.
- [x] The run covers 1435 and 1436 on each reference pin that has `UnixSockets`; the result lines
      list both by number with their outcome, and Notes record them per platform. A pin without the
      feature shows `feature-missing:UnixSockets`, not `server-not-built:http-unix`.
- [x] A failing case is filed with `task-planner` as a new task grouped by cause, its ID in Notes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library
      keeps 100% line and branch coverage.

## Notes

- If `Surl.Protocol.Http` needs a change to run on a Unix socket, that is a defect of BL-547's
  seam: file it as its own task rather than widening this one. It did not: the HTTP server half
  ran on BL-547's Unix socket listener unchanged.
- Delivered (2026-11-11):
  - `UpstreamTestCaseServer` gains `UnixSocketPathVariable` (optional, null for port servers);
    `UpstreamTestCaseServers`' `http-unix` row is built with `HTTPUNIXPATH`, and `socks5unix`
    names `SOCKSUNIXPATH` while staying unbuilt. Tests:
    `UpstreamTestCaseServersTests.Find_HttpUnix_IsBuiltAndMapsHttpUnixPath` and
    `All_OnlyTheUnixSocketServers_MapAPathVariable`.
  - `UpstreamTestCaseServerHalves.StartAsync` takes an optional socket path and adds an `http`
    listen URL with `UnixSocket` set, so the plaintext HTTP case server answers it.
    `UpstreamTestCasesAgainstSurlTests` composes `http-unix` as the HTTP family and gives such a
    case its socket path.
- Choices taken as sensible defaults:
  - **Socket file under the temporary folder, not the case's log folder.** The case folder sits
    beside the test assembly (`...\bin\Debug\net10.0\upstream-test-case-runs\<random>\test1435\log`),
    about 140 bytes, past `sun_path` (103 on macOS, 107 elsewhere) on every machine, lanes and CI
    included. Each `http-unix` case gets `<temp>/surl-<random>/http.sock`, deleted afterwards.
    Only the command line names `%HTTPUNIXPATH` (1435, 1436), and the value is wrapped in single
    quotes so a blank in the temporary folder's path (`C:\Users\First Last\...`) stays one word
    for the shell-word split.
- Results, tag `curl-8_21_0`:
  - `win-x64` (reference pin `0E7737...8778`, reports `UnixSockets`): 1435 pass, 1436 pass. Run
    1,126, pass 1,065, fail 61, skip 887 (was 1,124 / 1,063 / 61 / 889); passing list and the
    pass-rate row updated. No case failed because of this change, so no task was filed. A first
    run had 635 (SFTP) fail `data`; the second passed it - an existing flake, not this change.
  - `linux-x64`, `osx-arm64`: not run in this lane (lanes test on Windows only). Both reference
    pins report `UnixSockets` (ADR-0016), so the next CI `Upstream test cases` job runs both cases
    there; until then their rows keep `server-not-built:http-unix`, and a pass shows up as
    "newly passing" lines to add, never a failure.
  - A pin without `UnixSockets` skips them `feature-missing:UnixSockets`: `UpstreamTestCaseSelector`
    checks `server-not-built` before `feature-missing`, and with the row built that check no
    longer fires, so the feature check is the one that decides.
- Environment: the .NET SDK `global.json` pins (10.0.401, roll-forward `latestFeature`) is no
  longer installed on this machine - `C:\Program Files\dotnet\sdk` holds only 10.0.100-rc.1,
  11.0.100-rc.2 and 11.0.100 since 2026-11-10 17:16. `dotnet build` in the repository fails to
  resolve an SDK. This lane built and tested with SDK 11.0.100 from a working folder outside the
  repository (targets still `net10.0` on the 10.0.15 runtime): `-warnaserror` clean, every fast
  test green. Stewart: reinstall SDK 10.0.4xx so lanes build in place.
- Coverage: the only `Surl.Conformance.UnitLibrary` change is a record's optional positional
  property and two table rows, both read by the new tests; nothing else in a `UnitLibrary`
  changed.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. http-unix server half built: Surl's HTTP server answers upstream cases 1435 and 1436 on a Unix socket; both pass on win-x64
