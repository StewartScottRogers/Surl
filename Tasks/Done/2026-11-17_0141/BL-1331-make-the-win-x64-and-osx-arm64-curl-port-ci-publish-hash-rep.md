---
id: BL-1331
title: Make the win-x64 and osx-arm64 Curl port CI publish hash reproducible across runs (runner toolchain drifts)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [CurlPortBuilds.json, .github/workflows/curl-port-phase-7.yml, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1331 — Make the win-x64 and osx-arm64 Curl port CI publish hash reproducible across runs

## Goal

`build-curl-port` passes on win-x64 and osx-arm64 on every run, not only the run a pin was taken from.

## Context

Within one run both publishes agree, but across runs of the same Curl port commit 74f3f45c the hash moves (runner image toolchain):
- win-x64: run 43995901070 gave 4B634536..., run 43997573549 gave 5ECBCAB5199E234844168C607694059BF4AD60A7E373A3CC9298D631D7C9D93E.
- osx-arm64: run 43995901070 gave B5B48DC7..., run 43997573549 gave E2E186CC2B524F896CB52DB21D65470C931F0BAEADBD556E16A58BD9A7C13807.
linux-x64 passes. Re-pinning (BL-1329) cannot hold. Find what varies (SDK patch, runtime pack, workload) and pin it in the workflow (global.json / setup-dotnet version / ADR-0175), or change the check to compare the two publishes only for these platforms; record in ADR-0175.

## Acceptance criteria

- [x] The workflow and pin change are in place (ADR-0175 Amendment 2); two dispatched runs confirming it are filed as a follow-up, since a lane cannot dispatch.

## Notes

ADR-0175 Amendment 2: `ciHashStable: false` on win-x64 and osx-arm64; the job compares only its two publishes. touches widened to the workflow and ADR folder (no other Doing task names them). The acceptance run needs a pushed dispatch, which a lane cannot do; confirm with two `gh workflow run curl-port-phase-7` runs.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Workflow compares only in-run publishes for win-x64 and osx-arm64 (ADR-0175 Amendment 2); CI confirmation filed as BL-1332
