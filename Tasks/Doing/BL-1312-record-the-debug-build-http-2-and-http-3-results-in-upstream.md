---
id: BL-1312
title: Record the Debug-build http/2 and http/3 results in Upstream-Test-Case-Pass-Rate.md (ADR-0173)
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1311, BL-1317]
touches: [Documentation/Conformance]
requirement: none
created: 2026-11-16
completed:
---
# BL-1312 — Record the Debug-build http/2 and http/3 results in Upstream-Test-Case-Pass-Rate.md (ADR-0173)


## Goal

`Upstream-Test-Case-Pass-Rate.md` states the Linux numbers after the Debug-build cases and says why Windows and macOS are unchanged.

## Context

ADR-0173 consequences. `UpstreamTestCasePassRateStatementTests` ties the table to the passing list.

## Acceptance criteria

- [ ] The `linux-x64` row's Runnable, Passed, Failed and Skipped match a run with the Debug build, and `debug-build` in the skipped-by-reason table falls by the cases now run.
- [ ] A paragraph names ADR-0173, the six cases and the four libtest cases still skipped.
- [ ] `UpstreamTestCasePassRateStatementTests` passes.

## Notes

- The Linux numbers need a run with the Debug curl, which exists only in CI's Linux job (BL-1311, BL-1317). Nothing to record from this Windows lane until BL-1317 supplies the run.

## Log

- 2026-11-16: Created by BL-1303 (ADR-0173).
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. Needs the Linux Debug-build run from BL-1317; depends-on now includes BL-1317
- 2026-11-16: Backlog -> Doing.
