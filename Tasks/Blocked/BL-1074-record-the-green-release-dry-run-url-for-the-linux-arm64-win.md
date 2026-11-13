---
id: BL-1074
title: Record the green release dry run URL for the linux-arm64, win-arm64 and osx-x64 conformance legs
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-361, BL-1108]
touches: [.github/actions/upstream-curl/action.yml, .github/workflows/release.yml, Tasks]
requirement: none
created: 2026-11-12
completed:
---
# BL-1074 — Record the green release dry run URL for the linux-arm64, win-arm64 and osx-x64 conformance legs

## Goal

The release dry run's conformance job is green for `linux-arm64`, `win-arm64` and `osx-x64`, and BL-361's Notes record the run URL.

## Context

BL-361 wired those three legs (ADR-0151) but a lane cannot push, so no run of them existed when it finished. Fix a leg that fails (the action's Windows steps are untested on a runner) and record the URL.

## Acceptance criteria

- [ ] A `Release` workflow dry run on the integrated branch shows `Conformance against the published linux-arm64`, `win-arm64` and `osx-x64` green, and its URL is in BL-361's Notes.
- [ ] Any leg that failed is fixed in `.github/actions/upstream-curl/action.yml` or `.github/workflows/release.yml`, or its failing tests are filed as tasks.

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Blocked. Needs a pushed branch and a GitHub Actions Release dry run; a lane cannot push or run workflows, so nobody can record the green URL until the shift integrates and someone runs it
- 2026-11-13: Release run 43443817332 (PR #32, 810f09e): linux-arm64 and osx-x64 conformance green; win-arm64 failed 6 Windows reference-build tests, filed as BL-1108. Still blocked: needs a Release run on a commit that has BL-1108.
