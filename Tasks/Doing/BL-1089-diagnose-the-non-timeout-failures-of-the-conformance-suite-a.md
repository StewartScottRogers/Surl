---
id: BL-1089
title: Diagnose the non-timeout failures of the Conformance suite against a published win-x64 surl (data-directory restart, test841, port-defect lists)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-12
completed:
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

- [ ] Notes explain each failure above with evidence.
- [ ] Two consecutive full runs with `SURL_EXECUTABLE` on a win-x64 publish have no failure; Notes record counts.
- [ ] Fast tests stay green.

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
