---
id: BL-2119
title: Say in NFR-002, NFR-003 and NFR-005 where Measure-CodeQuality.ps1 is actually run
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2119 — Say in NFR-002, NFR-003 and NFR-005 where Measure-CodeQuality.ps1 is actually run

## Goal

NFR-002, NFR-003 and NFR-005 say which runs of `Measure-CodeQuality.ps1` apply their gates and which runs only report.

## Context

Found while working BL-2112 (2026-11-28): the three rows in `Documentation/Product/Requirements.md` say "Enforced by `Measure-CodeQuality.ps1`". The script exits 1 on a gap, but `.github/workflows/ci.yml` does not run it, a dark factory lane's check before it pushes (`RunDarkFactory.ps1`) is build and fast tests, and `.github/workflows/gource.yml` (step "Measure coverage") runs it for the published report and ignores its exit code. Adding a run that blocks anything is not part of this task. Stand-in: nothing to add; documentation only.

## Acceptance criteria

- [x] NFR-002, NFR-003 and NFR-005 claim no enforcement that no workflow or script performs, and name where the script is run.

## Notes

- The three rows' status is now `Draft [waits on a blocking run: ...]`. The non-functional section allows only `Enforced by <gate>` or `Draft` with a reason, and no run blocks on these three gates, so `Draft` is the true word; the preamble is unchanged. NFR-002 names every run of the script; NFR-003 and NFR-005 refer to it.
- Checked: `ci.yml`, `release.yml` and `curl-port-phase-7.yml` do not run the script; `gource.yml` step "Measure coverage" runs it and exits 0; a lane's check in `RunDarkFactory.ps1` is build and fast tests; the `coverage-auditor` agent runs it on request.
- Seen outside this task and filed as BL-2120: NFR-007 claims `Directory.Packages.props` and the `ci.yml` build enforce the package count, but no check that would fail on a second package was found; NFR-008 to NFR-020 say `Draft` with no bracketed reason, which their preamble asks for; the `CodeMetricsConfig.txt` comment says the `coverage-auditor` agent "enforces" the CRAP limit, and the agent only reports and files tasks.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. NFR-002, NFR-003 and NFR-005 say where Measure-CodeQuality.ps1 runs and that no run blocks on its coverage or CRAP gates
