---
id: BL-359
title: Let the conformance tests run a published surl binary instead of the in-process entry point
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-358]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-359 — Let the conformance tests run a published surl binary instead of the in-process entry point

## Goal

When the setting that BL-358's ADR names is present, every test that uses `SurlOnLoopback` starts the
published `surl` binary as a process instead of calling the in-process entry point. The planning
assumption is an environment variable, `SURL_EXECUTABLE`, that holds the binary's path. The same
conformance suite then proves the native binary.

## Context

- Decision: decision 2 of BL-358's ADR. If the ADR names another setting or shape, follow the ADR.
- `Conformance/Surl.Conformance.UnitTests/SurlOnLoopback.cs` runs surl in-process through the `Surl.Console` entry
  point. It waits for each `Listening on ` status line and exposes `BaseUrl`, `BaseUrls`, `Log`
  (surl's error stream) and `ServedDirectory`. It stops surl by cancelling a `CancellationTokenSource`.
  A process needs another way to stop: read how `Surl.Console` ends on Ctrl+C or SIGTERM before
  choosing one.
- With the setting absent nothing changes: every existing test keeps its in-process run.

## Acceptance criteria

- [x] With the setting unset, `SurlOnLoopback` behaves as it does today.
- [x] With the setting naming the `surl.exe` that `dotnet publish Application/Surl.Console -c Release -r win-x64 -o <dir>`
      produced, this passes on Windows, with the result recorded in Notes:
      `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamCurlFetchesFromSurlOverHttp11Tests"`.
- [x] When the setting names a file that does not exist, each test fails with a message naming the
      path instead of hanging.
- [x] Disposing stops the process within the test's timeout, and kills it if it is still alive. No
      `surl` process outlives the test run.
- [x] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast tests are green.

## Notes

- Built as ADR-0085 decision 2 says: `SURL_EXECUTABLE`, unset or empty = in-process, unchanged.
  New `PublishedSurlProcess` starts the binary with the same arguments, waits up to 30 s for the
  `Listening on ` status lines, keeps stderr as `Log`, and on dispose sends SIGTERM through
  `kill -TERM` with 10 s grace on Linux and macOS; otherwise, and at once on Windows, it kills the
  process tree, then waits for exit. `SurlOnLoopback.StartServingAsync` picks in-process or
  process, and now also deletes its temporary directory when a start fails.
- Choice: the variable is read on every start, not cached. A missing file throws
  `FileNotFoundException` "SURL_EXECUTABLE names <path>, which does not exist." before anything
  starts, so no test hangs.
- `SurlOnLoopbackTests`: the missing-file test (fast), plus two Integration tests that run
  `surl.dll` through `DOTNET_HOST_PATH` as a stand-in binary. One checks the port is closed after
  dispose; the other checks that a surl exiting before it listens reports its exit code and stderr.
- Result 2026-11-04, Windows: `dotnet publish Application/Surl.Console -c Release -r win-x64 -o %TEMP%\surl-bl359-publish`;
  with `SURL_EXECUTABLE` naming its `surl.exe`, `UpstreamCurlFetchesFromSurlOverHttp11Tests`
  passed 6, failed 0, and no `surl` process from that path was left afterwards. With
  `SURL_EXECUTABLE=Z:\no\such\surl.exe` each test failed at once with
  `FileNotFoundException: SURL_EXECUTABLE names Z:\no\such\surl.exe, which does not exist.`
  Unset: the same 6 passed in-process.
- Full `dotnet build` clean; fast tests green (Surl.Conformance.UnitTests 564 passed).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. SURL_EXECUTABLE makes every SurlOnLoopback start run a published surl as a process; HTTP/1.1 conformance passes against native win-x64 surl.exe
