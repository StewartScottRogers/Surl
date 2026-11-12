---
id: BL-1090
title: Stop load-related flakes of the Conformance suite against a published win-x64 surl (SSH key exchange, libcurl RTSP scripts, upstream cases under 32 workers)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1090 — Stop load-related flakes of the Conformance suite against a published win-x64 surl (SSH key exchange, libcurl RTSP scripts, upstream cases under 32 workers)

## Goal

Two consecutive full runs of the Conformance suite with `SURL_EXECUTABLE` on a win-x64 publish pass, once BL-1088 has fixed the HTTP/3 bind.

## Context

BL-1089 (2026-11-12) fixed the start-up log race and the Curl-port connection-reuse flake. Its five full runs on a win-x64 publish still each failed differently, all under 32 method-level workers: run 1 clean (1873 passed); run 2 `HostKey_OtherSha256Pinned_Exits60` and an `in memory` SSH case ("Unable to exchange encryption keys"), plus the HTTP/3 bind failures; run 3 `OptionsThenSetup_AfterTeardown_AreAnsweredAndSetUpTheSameSessionId` (a 3 m 21 s libcurl RTSP script, Libcurl.md finding) and `RunAsync_EveryCaseWithAComposedServerHalf_WritesOneResultLinePerCase` (test626 errorcode); an earlier run test616 and test641 (ssh) errorcode and `Binary_Echo_ComesBackWhole` (a nested dotnet build failing on a locked `.git\config`). Find which are load (cap workers, retry the script start) and which are defects.

## Acceptance criteria

- [x] Notes explain each failure above with evidence.
- [x] Two consecutive full runs with `SURL_EXECUTABLE` on a win-x64 publish have no failure; Notes record counts.
- [x] Fast tests stay green.

## Notes

- Root cause of every listed failure: load. The suite ran at `Workers = 0` (32 workers on this machine), each test starting real processes (published surl, upstream curl, nested dotnet builds). Evidence: the failures moved from run to run (run 1 clean, run 2 SSH kex timeouts, run 3 a 3 m 21 s libcurl RTSP script and test626 errorcode, an earlier run test616/test641 and a nested dotnet build hitting a locked `.git\config`), none reproduced on the same case twice, and none were tied to a protocol code change. The nested-build lock and test616/626/641 timeouts are the same starvation/contention class; no deterministic defect was found.
- Decision: cap the Conformance suite at 8 method-level workers through its own `MSTestSettings.cs` (the shared root file is removed from that project only via `Compile Remove`). Other test projects keep one worker per processor. Small enough decision not to need an ADR; it is a test-harness setting.
- Result with `SURL_EXECUTABLE` on a fresh win-x64 native publish: run 1 Passed 1873, Failed 0, Skipped 120 of 1993 (9 m 5 s); run 2 Passed 1873, Failed 0, Skipped 120 of 1993 (9 m 6 s).

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Conformance suite capped at 8 workers; two consecutive full runs on a win-x64 publish pass (1873 passed each)
