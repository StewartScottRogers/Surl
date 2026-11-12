---
id: BL-604
title: Stop conformance tests against a published win-x64 surl timing out before the listening line under parallel load
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1035]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-604 — Stop conformance tests against a published win-x64 surl timing out before the listening line under parallel load

## Goal

The whole `Surl.Conformance` suite passes against a published `win-x64` surl (`SURL_EXECUTABLE`) on
Stewart's machine, as ADR-0085 decision 2 requires before a release tag, with no test failing on a
start timeout.

## Context

- 2026-11-04, BL-360, on the dark factory machine with six lanes building: a native `win-x64`
  publish passed `Test-PublishedSurl.ps1`, but three full runs of
  `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~Surl.Conformance"`
  with `SURL_EXECUTABLE` set failed 5, 16 and a similar number of tests (of 1050), each a different
  set, each a `System.TimeoutException` after 30 s: `SurlOnLoopback.StartTimeout` expiring in
  `PublishedSurlProcess.WaitUntilListeningAsync` before the `Listening on` line. WebSocket and RTSP
  tests and `SurlOnLoopbackTests.StartPublishedAsync_Process_ListensAndStopsOnDispose` were among
  them. The same tests pass when run alone, in-process and against the binary.
- Find the cause before changing a number: a pipe not drained (surl blocking on a full standard
  error), process start cost under parallel test workers, or antivirus scanning each launch. Raising
  the timeout alone hides a deadlock if that is what it is.

## Acceptance criteria

- [ ] Notes name the cause, with the evidence for it.
- [ ] Two consecutive full runs of the conformance suite with `SURL_EXECUTABLE` naming a `win-x64`
      publish have no failure, and Notes record their counts.
- [ ] The fast tests stay green.

## Notes

- 2026-11-11, lane 3: no cause found yet. Read so far: `PublishedSurlProcess` drains both pipes with `BeginOutputReadLine`/`BeginErrorReadLine`, so a full standard error cannot block surl; MSTest runs at `Workers = 0` (one per core, 32 here), method level, so up to 32 surl and curl processes start at once. Could not publish win-x64 to reproduce: `dotnet publish` fails, the pinned SDK 10.0.401 is not installed (BL-1035).

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so surl cannot be published or built to reproduce the timeouts
- 2026-11-11: Backlog -> Doing.
