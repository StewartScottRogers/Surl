---
id: BL-603
title: Dispatch the release dry run on the integrated BL-360 commit and record it green on all six runtime identifiers
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-360]
touches: [.github/workflows/release.yml, .github/workflows/ci.yml, .github/actions, Test-PublishedSurl.ps1]
requirement: none
created: 2026-11-04
completed:
---
# BL-603 — Dispatch the release dry run on the integrated BL-360 commit and record it green on all six runtime identifiers

## Goal

A `workflow_dispatch` run of `release.yml` on the commit that integrated BL-360 is green on all six
runtime identifiers, and this task's Notes record its run URL.

## Context

- BL-360 wrote `Test-PublishedSurl.ps1`, `.github/actions/upstream-curl` and the new `release.yml`
  (ADR-0085). A dark factory lane cannot push, so it could not dispatch the run itself; this task is
  BL-360's third acceptance criterion, moved here.
- Not proven before the run: the `ubuntu:22.04` container leg of the smoke test (Docker on
  `ubuntu-22.04` and `ubuntu-22.04-arm`), the `macos-15-intel` leg, PowerShell 7 on the Arm
  images, and the conformance jobs against the published binary. BL-360 proved the script on
  `win-x64` only, locally.
- Start: `gh workflow run release.yml --ref master`, then `gh run watch`. Fix what fails in the
  files under `touches`.

## Acceptance criteria

- [ ] A `workflow_dispatch` run of `release.yml` is green on all six `Publish` legs and both
      `Conformance against the published` jobs, and Notes record its run URL.

## Notes

## Log

- 2026-11-04: Created.
