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
completed:
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

- [ ] `PinnedCurlPortClientOnThisMachineTests.RunAsync_PinnedClient_RunsVersionAndExitsZero` (or a name saying
      the same) exists, is `[TestCategory("Integration")]`, calls `PinnedCurlPortClientOnThisMachine.RunAsync`
      with `--version` and asserts exit code 0.
- [ ] On Stewart's Windows machine, with BL-577's pin published, the test passes (not Inconclusive).
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
