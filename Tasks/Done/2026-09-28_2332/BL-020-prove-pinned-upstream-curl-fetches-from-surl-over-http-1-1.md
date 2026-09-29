---
id: BL-020
title: Prove pinned upstream curl fetches from surl over HTTP/1.1
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-007, BL-019]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-020 — Prove pinned upstream curl fetches from surl over HTTP/1.1

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` start `surl`
in-process on loopback, run the pinned upstream curl 8.21.0 build against it, and assert
curl's exit code and output for each Phase 1 HTTP case. This is Phase 1's proof that
upstream curl fetches from surl.

## Context

- Product overview, Phase 1 "Proves": `surl http://...` serves and upstream curl fetches
  from it. Success criterion 1: curl's exit code, output and the bytes on the wire are
  what a correct server produces.
- ADR-0003: only a pinned build runs, never the Curl port. BL-007's locator finds and
  verifies it. Where no pinned build exists for the platform (Linux and macOS today),
  each test calls `Assert.Inconclusive` with the locator's reason.
- BL-019 added an internal `surl` entry point taking arguments, writers and a
  `CancellationToken`. `Surl.Conformance.UnitTests` references `Surl.Console` and has
  `InternalsVisibleTo`. The bound port comes from the status line (BL-016's format, from
  the command-line ADR recorded by BL-003).
- The process runner goes in `Surl.Conformance.UnitLibrary`. It starts the located
  build with `ProcessStartInfo.ArgumentList`, captures stdout as bytes and stderr as
  text, and enforces a timeout. Process-starting members carry
  `[ExcludeFromCodeCoverage]` with a justifying comment, because the fast tests start no
  process. Argument building and result shaping are fast-tested.
- `--fail` gives exit 22 (`CURLE_HTTP_RETURNED_ERROR`) on status 400 or above
  (https://curl.se/libcurl/c/libcurl-errors.html, checked 2026-09-28).
- The expected results for each case are the ones BL-018 recorded when it fed surl's
  responses to the pinned build. This task proves the live server produces them.

## Acceptance criteria

- [x] A curl-process runner in `Surl.Conformance.UnitLibrary` runs only a build BL-007's
      locator returned, and returns exit code, stdout bytes and stderr text. Fast tests
      cover its argument handling and timeout result.
- [x] Integration tests, each serving a fresh temporary directory holding `hello.txt`
      and `empty.txt`, assert:
      `curl -s http://127.0.0.1:<P>/hello.txt` exits 0 and stdout equals the file's
      bytes; `curl -s http://127.0.0.1:<P>/empty.txt` exits 0 with empty stdout;
      `curl -sI http://127.0.0.1:<P>/hello.txt` exits 0 and stdout's first line is an
      HTTP/1.1 200 status line; `curl -s -0 http://127.0.0.1:<P>/hello.txt` exits 0 with
      the file's bytes; `curl -s --fail http://127.0.0.1:<P>/missing` exits 22;
      `curl -s http://127.0.0.1:<P>/hello.txt http://127.0.0.1:<P>/hello.txt` exits 0 and
      stdout is the file's bytes twice.
- [x] On Windows with the pinned build present, `dotnet test --filter
      "FullyQualifiedName~Surl.Conformance"` (Integration included) is green, and the
      test output names the build's SHA-256.
- [x] `dotnet build Surl.Conformance.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Conformance.UnitLibrary`.
- [x] Any case where the pinned build disagrees with surl is fixed in
      `Surl.Protocol.Http` through a new task filed by `task-planner`, never by changing
      the expected result to match surl. Such tasks are listed in this task's Log.

## Notes

- `UpstreamCurlRunner` takes an `UpstreamCurlLocation` rather than a path, so it cannot
  start a curl the locator did not verify; a location with no build is an
  `ArgumentException` carrying the locator's reason. Only `RunAsync` (it starts a process)
  is `[ExcludeFromCodeCoverage]`; start information and both result shapes
  (`Exited`, `StoppedAtTimeout`) are fast-tested.
- The timeout runs on an injected `TimeProvider`; at the timeout curl's whole process tree
  is killed and the result keeps what it had written, with no exit code. Default in the
  tests: 30 seconds per run - generous for loopback, short enough that a hung server
  fails the test instead of the shift.
- stdin is redirected and closed so curl never waits on it; stderr is read as UTF-8.
- `SurlOnLoopback` (test side) starts surl through `Program.RunAsync` on
  `http://127.0.0.1:0/` and reads the bound port from the status line, as the Console
  integration test does. Each test serves its own temporary directory.
- Measured 2026-09-28 against the pinned win-x64 build
  (SHA-256 0E773709...8778): all six cases agree with surl, so no `Surl.Protocol.Http`
  follow-up task was needed.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Pinned upstream curl 8.21.0 fetches from a live in-process surl over HTTP/1.1: six Integration cases green, fast-tested curl runner in Surl.Conformance
