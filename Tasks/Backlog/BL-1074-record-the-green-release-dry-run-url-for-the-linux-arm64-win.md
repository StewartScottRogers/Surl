---
id: BL-1074
title: Record the green release dry run URL for the linux-arm64, win-arm64 and osx-x64 conformance legs
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-361]
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
