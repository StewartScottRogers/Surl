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
completed:
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

- [ ] A curl-process runner in `Surl.Conformance.UnitLibrary` runs only a build BL-007's
      locator returned, and returns exit code, stdout bytes and stderr text. Fast tests
      cover its argument handling and timeout result.
- [ ] Integration tests, each serving a fresh temporary directory holding `hello.txt`
      and `empty.txt`, assert:
      `curl -s http://127.0.0.1:<P>/hello.txt` exits 0 and stdout equals the file's
      bytes; `curl -s http://127.0.0.1:<P>/empty.txt` exits 0 with empty stdout;
      `curl -sI http://127.0.0.1:<P>/hello.txt` exits 0 and stdout's first line is an
      HTTP/1.1 200 status line; `curl -s -0 http://127.0.0.1:<P>/hello.txt` exits 0 with
      the file's bytes; `curl -s --fail http://127.0.0.1:<P>/missing` exits 22;
      `curl -s http://127.0.0.1:<P>/hello.txt http://127.0.0.1:<P>/hello.txt` exits 0 and
      stdout is the file's bytes twice.
- [ ] On Windows with the pinned build present, `dotnet test --filter
      "FullyQualifiedName~Surl.Conformance"` (Integration included) is green, and the
      test output names the build's SHA-256.
- [ ] `dotnet build Surl.Conformance.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Conformance.UnitLibrary`.
- [ ] Any case where the pinned build disagrees with surl is fixed in
      `Surl.Protocol.Http` through a new task filed by `task-planner`, never by changing
      the expected result to match surl. Such tasks are listed in this task's Log.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
