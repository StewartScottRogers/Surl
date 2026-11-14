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
completed: 2026-11-13
---
# BL-1074 — Record the green release dry run URL for the linux-arm64, win-arm64 and osx-x64 conformance legs

## Goal

The release dry run's conformance job is green for `linux-arm64`, `win-arm64` and `osx-x64`, and BL-361's Notes record the run URL.

## Context

BL-361 wired those three legs (ADR-0151) but a lane cannot push, so no run of them existed when it finished. Fix a leg that fails (the action's Windows steps are untested on a runner) and record the URL.

## Acceptance criteria

- [x] A `Release` workflow dry run on the integrated branch shows `Conformance against the published linux-arm64`, `win-arm64` and `osx-x64` green, and its URL is in BL-361's Notes.
- [x] Any leg that failed is fixed in `.github/actions/upstream-curl/action.yml` or `.github/workflows/release.yml`, or its failing tests are filed as tasks.

## Notes

Green Release dry run: https://github.com/StewartScottRogers/Surl/actions/runs/43506096468 (PR #33, commit e6077e2, which has BL-1108). Every conformance leg passed: linux-arm64, linux-x64, win-arm64, osx-arm64 and osx-x64. BL-361 is archived (Tasks/Done/2026-11-12_0707), and archived tasks are final, so the URL is recorded here instead of in its Notes. The one leg that failed earlier, win-arm64 in run 43443817332, was fixed by BL-1108.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Blocked. Needs a pushed branch and a GitHub Actions Release dry run; a lane cannot push or run workflows, so nobody can record the green URL until the shift integrates and someone runs it
- 2026-11-13: Release run 43443817332 (PR #32, 810f09e): linux-arm64 and osx-x64 conformance green; win-arm64 failed 6 Windows reference-build tests, filed as BL-1108. Still blocked: needs a Release run on a commit that has BL-1108.
- 2026-11-13: Blocked -> Backlog. Unblocked: BL-1108 now Done
- 2026-11-13: Backlog -> Doing.
- 2026-11-13: Doing -> Blocked. Needs a Release workflow run on a commit containing BL-1108 (8030f331 is on origin/factory/phase-1; latest run 43443817332 predates it). A lane cannot push or dispatch workflows; the next merge PR's Release run, or a manual dispatch by Stewart, supplies the URL.
- 2026-11-13: Blocked -> Doing.
- 2026-11-13: Doing -> Done. Release run 43506096468 on e6077e2 is green on every conformance leg, win-arm64 included; URL in Notes
