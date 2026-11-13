---
id: BL-1089
title: Diagnose the non-timeout failures of the Conformance suite against a published win-x64 surl (data-directory restart, test841, port-defect lists)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1088, BL-1090]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1089 — Diagnose the non-timeout failures of the Conformance suite against a published win-x64 surl

## Goal

Each failure below is explained (flaky test, surl defect filed as its own task, or stale list) and the suite is clean against a win-x64 publish.

## Context

BL-604 full runs against a win-x64 publish (2026-11-12) had no start timeout left, but failed besides the HTTP/3 bind (BL-1088):
- `PublishRestartSubscribe_DataDirectory_SubscriberReceivesWhatTheRecordingHolds` (subscriber got 0 bytes) and `PublishOnOneListener_RestartOverTheDataDirectory_SubscribeOnAnother_...` (element count differs).
- `GetWithInsecure_SameFileTwice_ExitsZeroWithTheFilesBytesTwice`: "A port defect Http.md does not list".
- `RunBesideAsync_EveryCaseOnThePassingList_PortDefectsAreTheListedFindings`: test841 (mail) upstream fail, port pass; port-test884 line 3 differs.
Run each alone first, to tell load flakiness from defects.

## Acceptance criteria

- [x] Notes explain each failure above with evidence.
- [x] Two consecutive full runs with `SURL_EXECUTABLE` on a win-x64 publish have no failure; Notes record counts. (Done by BL-1090: two clean runs, 1873 passed each.)
- [x] Fast tests stay green.

## Notes

2026-11-12, lane 4, on a fresh win-x64 publish. Run alone, the two data-directory restart tests pass, so they were load flakes, not defects; no further evidence of a restart defect.
- `GetWithInsecure_SameFileTwice...` (Curl-port mirror, not a surl defect): intermittent, 2 of 6 alone, also in-process. Upstream curl opens two exchanges; the Curl port sometimes opens one, so the compared surl notes differ at line 3. A Curl-port connection-reuse nondeterminism. Fix: that one case compares notes `WithoutTlsEndingsAndExchangeOpenings` (new in `CurlPortRunSetup`); exit code and bytes still judge it. 8 of 8 passes after. Decided by Claude under Stewart's delegation (an ADR was not written: it changes one test's comparison, not a rule).
- `RunBesideAsync_EveryCaseOnThePassingList...` (test841/port-test884): passes alone 4 of 4 and passed in the second full run; failed only under load. Load flake, stale list ruled out.
- New, found while diagnosing: `surl's log has no Serving SSH host key ssh-rsa note` (Scp/Sftp tests): the note goes to stderr before `Listening on` on stdout, but separate pipe callbacks run late under load. `PublishedSurlProcess` now waits until stderr is quiet (two 25 ms checks, 5 s cap) after the status line. Not seen again in runs 1-3 after.
- Full runs after the fixes: 1873 passed, 0 failed (120 skipped); then 13 failed (11 HTTP/3 `Could not bind`, BL-1088; 2 SSH "Unable to exchange encryption keys"); then 2 failed (libcurl RTSP script finding, test626). No start timeout. Fast tests: 893 passed.
- The remaining flakes went to BL-1090, which capped the suite at 8 workers; its two consecutive full runs on a win-x64 publish passed (1873 passed, 0 failed, 120 skipped each). BL-1088 no longer on the board.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Backlog. Cause of each listed failure found and two fixed; the two clean full runs wait on BL-1088 (HTTP/3 bind) and BL-1090 (remaining load flakes)
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Failures explained; two clean full runs recorded by BL-1090
