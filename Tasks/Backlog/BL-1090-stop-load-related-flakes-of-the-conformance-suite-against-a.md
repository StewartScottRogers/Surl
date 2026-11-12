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
completed:
---
# BL-1090 — Stop load-related flakes of the Conformance suite against a published win-x64 surl (SSH key exchange, libcurl RTSP scripts, upstream cases under 32 workers)

## Goal

Two consecutive full runs of the Conformance suite with `SURL_EXECUTABLE` on a win-x64 publish pass, once BL-1088 has fixed the HTTP/3 bind.

## Context

BL-1089 (2026-11-12) fixed the start-up log race and the Curl-port connection-reuse flake. Its five full runs on a win-x64 publish still each failed differently, all under 32 method-level workers: run 1 clean (1873 passed); run 2 `HostKey_OtherSha256Pinned_Exits60` and an `in memory` SSH case ("Unable to exchange encryption keys"), plus the HTTP/3 bind failures; run 3 `OptionsThenSetup_AfterTeardown_AreAnsweredAndSetUpTheSameSessionId` (a 3 m 21 s libcurl RTSP script, Libcurl.md finding) and `RunAsync_EveryCaseWithAComposedServerHalf_WritesOneResultLinePerCase` (test626 errorcode); an earlier run test616 and test641 (ssh) errorcode and `Binary_Echo_ComesBackWhole` (a nested dotnet build failing on a locked `.git\config`). Find which are load (cap workers, retry the script start) and which are defects.

## Acceptance criteria

- [ ] Notes explain each failure above with evidence.
- [ ] Two consecutive full runs with `SURL_EXECUTABLE` on a win-x64 publish have no failure; Notes record counts.
- [ ] Fast tests stay green.

## Notes

## Log

- 2026-11-12: Created.
