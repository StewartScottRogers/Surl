---
id: BL-1647
title: Decide how Phase 7 leaves out test251's timing-dependent verdict
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Conformance/CurlPort/Upstream-Test-Cases.md]
requirement: FR-057
created: 2026-11-24
completed: 2026-11-24
---
# BL-1647 — Decide how Phase 7 leaves out test251's timing-dependent verdict

## Goal

An ADR amendment decides whether and how Phase 7 leaves out the timing-dependent verdict of upstream test case test251 (UTC-4), so the linux-x64 upstream-test-cases result is the same from run to run.

## Context

`test251` (FTP active mode, `-P`, a deliberately slow server) shows its 60-second timeout as UTC-4 in runs 44044472038 and 45412760425 but finished in run 45198867449, so `RunBesideAsync_EveryCaseOnThePassingList_PortDefectsAreTheListedFindings` reported it stale there. No ADR-0089 amendment covers a timing race, so BL-1626 left it listed. Decide (ADR-0089 amendment, Decided by Claude under Stewart's delegation) whether the case is left out of the comparison or tolerated either way, and say so in `Upstream-Test-Cases.md`. Judge by pinned upstream curl, never the Curl port (ADR-0003).

## Acceptance criteria

- [x] An ADR-0089 amendment names test251 and the rule applied, and `Upstream-Test-Cases.md` UTC-4 states it.

## Notes

- Decision: tolerated either way (ADR-0089 amendment 10); stays measured, not left out. Code change split into BL-1649 (touches Conformance projects outside this docs task).

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. ADR-0089 amendment 10 and UTC-4 text written
