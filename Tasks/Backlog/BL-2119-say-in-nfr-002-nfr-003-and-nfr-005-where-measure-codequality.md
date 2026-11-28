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
completed:
---
# BL-2119 — Say in NFR-002, NFR-003 and NFR-005 where Measure-CodeQuality.ps1 is actually run

## Goal

NFR-002, NFR-003 and NFR-005 say which runs of `Measure-CodeQuality.ps1` apply their gates and which runs only report.

## Context

Found while working BL-2112 (2026-11-28): the three rows in `Documentation/Product/Requirements.md` say "Enforced by `Measure-CodeQuality.ps1`". The script exits 1 on a gap, but `.github/workflows/ci.yml` does not run it, a dark factory lane's check before it pushes (`RunDarkFactory.ps1`) is build and fast tests, and `.github/workflows/gource.yml` (step "Measure coverage") runs it for the published report and ignores its exit code. Adding a run that blocks anything is not part of this task. Stand-in: nothing to add; documentation only.

## Acceptance criteria

- [ ] NFR-002, NFR-003 and NFR-005 claim no enforcement that no workflow or script performs, and name where the script is run.

## Notes

## Log

- 2026-11-28: Created.
