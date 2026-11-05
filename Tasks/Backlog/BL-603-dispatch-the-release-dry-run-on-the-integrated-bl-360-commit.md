---
id: BL-603
title: Dispatch the release dry run on the integrated BL-360 commit and record it green on all six runtime identifiers
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-360, BL-832]
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

- 2026-11-05 (lane 8): BL-360 is on `factory/phase-1`, not `master` (`origin/master` is still
  eab39be6), so the dispatch must use `--ref factory/phase-1` (or `master` once a shift merges).
  No dispatch was made this run: a fix had to land first, and with ~70 Actions runs queued the
  run could not finish inside one lane's session.
- The `pull_request` Release runs on PR #18 already exercise every leg. Latest finished one,
  https://github.com/StewartScottRogers/Surl/actions/runs/42041978888: win-x64, win-arm64,
  osx-x64, osx-arm64 green (so `macos-15-intel` and PowerShell 7 on the Arm images work);
  two real failures:
  1. linux-x64 and linux-arm64, check 3 in the `ubuntu:22.04` container:
     `surl: (23) Could not create /served/.surl: Read-only file system`. `--directory` keeps its
     lock in `<path>/.surl/` and refuses a read-only directory by design (`surl --aihelp content`),
     but `Test-PublishedSurl.ps1` mounted the served directory `:ro`. Fixed in this lane: the
     served directory is mounted writable, and the container runs as the runner's own uid:gid
     (`--user`) so the `.surl` folder it creates is removable afterwards. The linux-x64
     conformance job failed only because that leg uploaded no artifact.
  2. Conformance against the published osx-arm64: `IOException: Broken pipe` writing upstream
     curl's standard input in `UpstreamCurlRunner` (a harness race, not a Surl defect). That is in
     `Conformance/Surl.Conformance.*`, which BL-396 holds in Doing, so it is filed as BL-832 and
     this task waits on it.
- Next run: once BL-832 is Done and lane 8's fix is integrated, `gh workflow run release.yml
  --ref <branch holding both>`, `gh run watch`, record the URL here.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Waits on BL-832 (UpstreamCurlRunner broken pipe in Conformance, held by BL-396); Linux container smoke check fixed in Test-PublishedSurl.ps1
