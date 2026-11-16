---
id: BL-1311
title: Run the Debug-feature http/2 and http/3 upstream test cases with the Debug build (ADR-0173)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1310]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1311 — Run the Debug-feature http/2 and http/3 upstream test cases with the Debug build (ADR-0173)


## Goal

On `linux-x64`, the upstream test cases 2400, 2401, 2406, 2501, 358 and 359 run with the Debug build and each either passes or is a recorded finding.

## Context

ADR-0173 decisions 6 and 7. `debug-build` is the skip reason today (ADR-0080). The runner picks the pinned Debug build only for a case whose `<features>` contain `Debug` and only when the platform pins one.

## Acceptance criteria

- [x] The suite chooses the supplementary Debug build for a case listing `Debug` on a platform that pins one, and still skips `debug-build` elsewhere; a fast test pins both branches.
- [x] No case outside the six is moved off its reference build (`UpstreamTestCaseDebugBuildCases.Numbers`, pinned by a fast test); fast tests: 1002 pass.

## Notes

- The suite's Linux run, the passing-list update and the pass-rate row moved to BL-1317: the Debug curl exists only in CI's Linux job, not on this Windows lane (no `/opt/upstream-curl/8.21.0-debug` in WSL either).
- Built: `UpstreamCurlLocator.LocateDebug`; `UpstreamCurlBuildFeatures` lists `Debug`/`TrackMemory` for a pin that prints them; the selector skips `debug-build` only when the build lacks the feature; the run plays the six cases with the Debug build where one is pinned and present.

## Log

- 2026-11-16: Created by BL-1303 (ADR-0173).
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Suite plays the six Debug-feature http/2 and http/3 cases with the pinned Debug build where one exists; Linux run split to BL-1317
