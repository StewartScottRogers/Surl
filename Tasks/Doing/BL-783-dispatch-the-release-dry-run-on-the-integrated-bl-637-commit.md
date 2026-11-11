---
id: BL-783
title: Dispatch the release dry run on the integrated BL-637 commit and record surl-mcp green on all six runtime identifiers
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-637]
touches: [.github/workflows/release.yml, Test-PublishedSurl.ps1]
requirement: none
created: 2026-11-05
completed:
---
# BL-783 — Dispatch the release dry run on the integrated BL-637 commit and record surl-mcp green on all six runtime identifiers

## Goal

A `workflow_dispatch` run of `release.yml` on a commit that holds BL-637's change is green on all six
`Publish` legs, `Publish surl-mcp`, `Smoke test surl-mcp` and `Package surl-mcp` included, and this task's
Notes record its run URL.

## Context

- BL-637 added `surl-mcp`'s publish, smoke test and package steps to `release.yml` (ADR-0103 decision 7).
  A dark factory lane cannot push, so it could not dispatch the run itself; this task is BL-637's second
  acceptance criterion, moved here, as BL-603 is BL-360's.
- Proved before the run: the native `win-x64` publish and the smoke step's script, locally (BL-637 Notes).
  Not proved: the five other legs, `shell: pwsh` on the Arm and Intel macOS images, and the `7z`/`tar`
  packaging of `surl-mcp`.
- Start: `gh workflow run release.yml --ref master`, then `gh run watch`. Fix what fails in `release.yml`.
  If BL-603 has not run yet, one green run satisfies both.

## Acceptance criteria

- [ ] A `workflow_dispatch` run of `release.yml` is green on all six `Publish` legs, each uploading both
      `surl-<rid>` and `surl-mcp-<rid>` artifacts, and Notes record its run URL.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
