---
id: BL-1320
title: Make the FTP max-connections timeout test hold its one connection before upstream curl dials on macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests/UpstreamCurlFetchesFromSurlOverFtpTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-1320 — Make the FTP max-connections timeout test hold its one connection before upstream curl dials on macOS

## Goal

`UpstreamCurlFetchesFromSurlOverFtpTests.Download_MaxConnections1WhileAnotherClientHoldsIt_ExitsOperationTimedOut` gives the same verdict on every run on every platform, including CI's "Conformance against the published osx-arm64" job.

## Context

- PR #94's run, 2026-11-16, "Conformance against the published osx-arm64": expected exit 28 (operation timed out), got 7, `curl: (7) Failed to connect to 127.0.0.1:49659 after 2 ms: Could not connect to server`. A rerun of the same job on the same commit passed, so the outcome depends on timing.
- Likely cause to check first: with a one-connection limit, how surl holds back the second connection (leaving it in the listen backlog vs. accepting and idling it vs. closing it) behaves differently on macOS when the backlog fills or the first client is not yet accepted when curl dials. Determine from upstream curl 8.21.0's behaviour (measure with `Record-CurlExchange.ps1` against the pinned build, ADR-0003) which of exit 28 and exit 7 a faithful server produces, and whether the test must wait until surl has accepted and is holding the first connection before starting curl.
- The mirror test `CurlPortBesideUpstreamCurlOverFtpTests.Download_MaxConnections1WhileAnotherClientHoldsIt_ExitsOperationTimedOut` follows whatever this fixes.
- Tests pass on Windows, Linux and macOS (CLAUDE.md): no sleep-based waits; wait on an observable condition (the event stream's connection event, or the listener's accepted count).

## Acceptance criteria

- [ ] The test waits on an observable condition that the first connection is held before upstream curl dials, with no `Thread.Sleep` or fixed delay.
- [ ] If the server's handling of a second connection over the limit changes, the change is justified by a pinned upstream curl 8.21.0 measurement recorded in the test or an ADR.
- [ ] The test passes 20 consecutive local runs on Windows, and CI's Linux and macOS conformance jobs pass on the task's commit.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-16: Created after the flaky failure on PR #94's osx-arm64 conformance job.
