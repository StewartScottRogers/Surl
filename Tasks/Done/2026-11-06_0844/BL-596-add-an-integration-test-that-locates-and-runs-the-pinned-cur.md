---
id: BL-596
title: Add an Integration test that locates and runs the pinned Curl port client
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-577]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-06
---
# BL-596 — Add an Integration test that locates and runs the pinned Curl port client

## Goal

`Surl.Conformance.UnitTests` has an `Integration` test that locates the Curl port client `CurlPortBuilds.json`
pins for this platform and runs it, so a missing or stale pin shows up as Inconclusive or a failure rather than
going unnoticed.

## Context

- BL-406 shipped `CurlPortClientLocator` and the test helper `PinnedCurlPortClientOnThisMachine`, but no test
  calls `PinnedCurlPortClientOnThisMachine.RunAsync` yet. BL-577 pinned the win-x64 client with
  `Publish-CurlPort.ps1` and checked the locator and runner by hand with a throwaway file-based app; it could not
  add the test because BL-389 held `Conformance/Surl.Conformance.UnitTests` at the time.
- The test is an ADR-0083 plumbing check only: it asserts the client was found and exited, never anything about
  its output as an expected value (ADR-0003, ADR-0083).
- Platform-neutral: on Linux and macOS no client is pinned yet, so the helper marks the test Inconclusive.

## Acceptance criteria

- [x] `PinnedCurlPortClientOnThisMachineTests.RunAsync_PinnedClient_RunsVersionAndExitsZero` (or a name saying
      the same) exists, is `[TestCategory("Integration")]`, calls `PinnedCurlPortClientOnThisMachine.RunAsync`
      with `--version` and asserts exit code 0.
- [x] On Stewart's Windows machine, with BL-577's pin published, the test passes (not Inconclusive).
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-11-06: Added `PinnedCurlPortClientOnThisMachineTests.RunAsync_PinnedClient_RunsVersionAndExitsZero` (Integration). It asserts only that the pinned client was found and exited 0 for `--version`, never anything about its output (ADR-0003, ADR-0083). On Stewart's Windows machine it passed (not Inconclusive) against the win-x64 pin at commit b42c319. Build with -warnaserror clean; fast tests green.

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. An Integration test now locates and runs the pinned Curl port client with --version and asserts exit 0; passes on Windows
