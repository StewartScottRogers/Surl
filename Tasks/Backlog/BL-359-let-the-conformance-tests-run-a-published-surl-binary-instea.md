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
completed:
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

- [ ] With the setting unset, `SurlOnLoopback` behaves as it does today.
- [ ] With the setting naming the `surl.exe` that `dotnet publish Application/Surl.Console -c Release -r win-x64 -o <dir>`
      produced, this passes on Windows, with the result recorded in Notes:
      `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamCurlFetchesFromSurlOverHttp11Tests"`.
- [ ] When the setting names a file that does not exist, each test fails with a message naming the
      path instead of hanging.
- [ ] Disposing stops the process within the test's timeout, and kills it if it is still alive. No
      `surl` process outlives the test run.
- [ ] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
