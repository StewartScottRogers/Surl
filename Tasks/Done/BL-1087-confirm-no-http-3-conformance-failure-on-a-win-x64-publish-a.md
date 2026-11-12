---
id: BL-1087
title: Confirm no HTTP/3 conformance failure on a win-x64 publish after the UDP bind retry (BL-1059)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Documentation/Conformance]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1087 — Confirm no HTTP/3 conformance failure on a win-x64 publish after the UDP bind retry (BL-1059)

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] HTTP/3 conformance tests on a win-x64 publish show no `Permission denied` (exit 45) failure.

## Notes

- 2026-11-12 (lane 4): Published win-x64 native surl (`dotnet publish Application/Surl.Console -r win-x64`, commit e405b969, includes BL-1059) and ran with `SURL_EXECUTABLE` set. HTTP/3 classes (`FullyQualifiedName~Http3`, 26 tests): 13 of 14 runs passed 26/26, no exit 45 or `Permission denied`; one run had 1 failure whose text was not captured (not reproduced in 12 later runs). Full `Surl.Conformance` run: 1872 passed, 120 skipped, 1 failed (11m46s); the one failure is the Curl-port Phase 7 finding in `UpstreamCurlFetchesFromSurlOverHttpsTests.GetWithInsecure_SameFileTwice...` (HTTPS, Curl port's behaviour), not HTTP/3. BL-1059's retry holds.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. win-x64 publish HTTP/3 conformance shows no UDP bind Permission denied after BL-1059
