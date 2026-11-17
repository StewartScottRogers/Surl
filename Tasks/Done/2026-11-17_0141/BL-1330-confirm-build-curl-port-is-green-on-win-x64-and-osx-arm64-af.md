---
id: BL-1330
title: Confirm build-curl-port is green on win-x64 and osx-arm64 after the BL-1329 re-pin
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1329, BL-1331]
touches: [CurlPortBuilds.json]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1330 — Confirm build-curl-port is green on win-x64 and osx-arm64 after the BL-1329 re-pin

## Goal

build-curl-port is green on win-x64 and osx-arm64 after the BL-1329 re-pin and the BL-1331 fix.

## Context

Run 43997573549 failed win-x64 and osx-arm64 because hashes differed run to run. BL-1331 (ADR-0175 Amendment 2) fixed it; BL-1332 dispatched two runs to confirm.

## Acceptance criteria

- [x] Runs 43998848915 and 44000208980 (curl-port-phase-7, workflow_dispatch) both concluded success, with "Build the Curl port" green on linux-x64, osx-arm64 and win-x64 (checked with `gh run view`).

## Notes

No file changed: the confirmation was already made by BL-1332's runs, re-verified here.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. Run 43997573549 failed win-x64 and osx-arm64: hashes differ run to run; waits on BL-1331
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Runs 43998848915 and 44000208980 green on all platforms, re-verified
