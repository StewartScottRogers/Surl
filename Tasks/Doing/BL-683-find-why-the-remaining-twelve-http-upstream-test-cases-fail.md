---
id: BL-683
title: Find why the remaining twelve http upstream test cases fail against Surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-392, BL-1035]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-683 — Find why the remaining twelve http upstream test cases fail against Surl

## Goal

Each of the http upstream test cases 30, 37, 60, 173, 329, 433, 470, 1064, 1065, 1147, 1244 and
2081 either passes against Surl's HTTP server half, or has its cause named and a task filed for it,
or is listed in Notes with the reason it cannot pass.

## Context

- Governing decision: ADR-0080 (`Documentation/Planning/Decisions/ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md`).
  Decision 2: the server half is Surl's own HTTP server sending the case's part verbatim. Decision 5:
  a case whose verified byte the upstream test server chose arbitrarily may be listed as a
  `test-server-choice` exclusion in `Documentation/Conformance/Upstream-Test-Case-Exclusions.md`
  (that file is outside this task's `touches`; file a `docs` task for any exclusion instead).
  Surl is written from FILEFORMAT.md, RFCs and measurement, never by translating upstream's test
  servers (`sws.c`); upstream curl 8.21.0 is the only oracle, measured with `Record-CurlExchange.ps1`.
- The run: `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamTestCasesOverHttp" --logger "console;verbosity=detailed"`
  (BL-392's `UpstreamTestCasesOverHttpTests`, categories `Integration` and `UpstreamTestCases`); it
  writes `upstream-test-case-results.win-x64.txt` and logs each failing case's first difference.
  Cases are in `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data/test<N>`.
- Where a cause may lie: the harness (`UpstreamTestCasesOverHttpTests`,
  `Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseCommandLine.cs`,
  `UpstreamTestCaseShellWords.cs`, `UpstreamTestCaseClientFile.cs`), the server half
  (`Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpUpstreamTestCaseServer.cs` and its readers), or
  a test-server choice.
- First differences on Windows, 2026-11-04:
  - 30, 37: timeout. curl waits 60 s; the case's `<data>` is empty, so the server half sends nothing
    and keeps the connection open.
  - 470: stderr gains `curl: (52) Empty reply from server`.
  - 1244: exit 52, expected 56.
  - 60: chunked upload; `<protocol>` line 9 expected `13`, missing.
  - 173: multipart `Content-Length` 372, expected 378.
  - 1064, 1065: stdout `Content-Length` 3, expected 9.
  - 1147: `<file>` verify: the output file is empty.
  - 329: the `Cookie` line lacks `tester=yes`.
  - 433: stderr `Note: Read config file from ''` path differs (XDG_CONFIG_HOME / `.curlrc` handling
    in the harness).
  - 2081: stdout gets the 301 reply where the 200 is expected.
- Fix a cause within this task only when it is small (a few lines in one of the touched projects);
  otherwise file a task per cause with `task-planner`, depending on this one where it needs its
  findings. Do not widen this task into every fix.

## Acceptance criteria

- [ ] Notes hold one line per case (30, 37, 60, 173, 329, 433, 470, 1064, 1065, 1147, 1244, 2081)
      naming its cause as harness, server half or test-server choice, with the evidence (the case
      text read, or the curl 8.21.0 bytes measured with `Record-CurlExchange.ps1` where bytes
      matter).
- [ ] Each of those cases is `pass` in `upstream-test-case-results.win-x64.txt` after the
      integration run above, or its Notes line names the ID of a task filed for its cause, or gives
      the reason it cannot pass; no previously passing case now fails.
- [ ] Every fix made here has a unit test in the touched project's `.UnitTests`, and the touched
      libraries keep 100% line and branch coverage.
- [ ] `dotnet build -warnaserror` is clean for each touched library and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- 2026-11-11 (lane 3): parked behind BL-1035. `global.json` pins SDK 10.0.401 and this machine
  has only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100, so `dotnet build` and the integration run
  this task needs cannot start. No work begun.

- 2026-11-12 (lane 4): SDK 10.0.404 is installed now; build and integration run work (~2 min).
  Causes, from the run's logged first differences:
  - 1064, 1065, 2081: now pass.
  - 30, 37: still `curl stopped after 60 s`; cause open -> BL-1075. (An unproven server-half
    change was tried, did not help.)
  - 60 (chunked upload line 9 `13` missing), 173 (multipart Content-Length 372 vs 378), 329 (Cookie
    lacks `tester=yes`) -> BL-1076.
  - 433 (config-file note path, harness), 470 (extra `curl: (52)` stderr), 1147 (output file
    empty), 1244 (exit 52 vs 56) -> BL-1077.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds
- 2026-11-12: Backlog -> Doing.
