---
id: BL-783
title: Dispatch the release dry run on the integrated BL-637 commit and record surl-mcp green on all six runtime identifiers
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-637, BL-1035]
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

- 2026-11-11 (lane 5): Dispatched https://github.com/StewartScottRogers/Surl/actions/runs/43034425678
  on master 424106dd (holds BL-637's 952492f5). Green: Publish win-x64, win-arm64, osx-x64, osx-arm64,
  each uploading `surl-<rid>` and `surl-mcp-<rid>`, so `shell: pwsh` on both macOS images and the
  `7z`/`tar` packaging of surl-mcp are proved. Red twice more on rerun: Publish linux-x64 and linux-arm64,
  at "Install the native toolchain": `apt-get` could not resolve `azure.archive.ubuntu.com`, an
  outage at the runner's apt mirror, not a fault in release.yml.
- The scheduled run on the same commit, https://github.com/StewartScottRogers/Surl/actions/runs/42626930936
  (2026-11-09), was green on all six Publish legs and uploaded all twelve `surl-*`/`surl-mcp-*` packages,
  linux-x64 and linux-arm64 included. It ran the same jobs, but from `schedule`, not `workflow_dispatch`.
- Fix written, uncommitted (the shift stashes it): the "Install the native toolchain" step runs apt only
  when `clang` or `/usr/include/zlib.h` is missing. The Ubuntu images already carry both (apt's log
  showed only the 3 kB `clang` meta-package as new), so a mirror outage can no longer fail a Linux leg.
- Cannot finish here: `dotnet build` fails because global.json pins SDK 10.0.401 and it is not installed,
  the same blocker that sent BL-758 and BL-778 behind BL-1035. Added BL-1035 to depends-on. Next run:
  commit the fix, and once it is on master, dispatch again and tick the box.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; linux release legs also hit an apt mirror outage (fix written, stashed)
