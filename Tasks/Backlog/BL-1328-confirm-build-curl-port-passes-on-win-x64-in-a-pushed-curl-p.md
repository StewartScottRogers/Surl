---
id: BL-1328
title: Confirm build-curl-port passes on win-x64 in a pushed curl-port-phase-7 run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1327, BL-1329]
touches: []
requirement: none
created: 2026-11-16
completed:
---
# BL-1328 — Confirm build-curl-port passes on win-x64 in a pushed curl-port-phase-7 run

## Goal

A pushed run of `curl-port-phase-7` shows `build-curl-port` green on win-x64.

## Context

BL-1327 added `ciSha256` to the win-x64 entry in `CurlPortBuilds.json` (ADR-0175) and made the workflow compare with it. A lane cannot push or dispatch, so the run is checked here. Dispatch with `gh workflow run curl-port-phase-7 --ref <branch>` and read it with `gh run list`.

## Acceptance criteria

- [ ] `gh run view` of a `curl-port-phase-7` run on a pushed commit containing ADR-0175 shows the win-x64 `build-curl-port` job passed ("The win-x64 publish matches the pin").
- [ ] If it failed, the cause is recorded under Notes and a fix task is filed.

## Notes

- 2026-11-16: Dispatched run 43995901070 on `factory/phase-1` (2d0d6a117, contains ADR-0175). win-x64 FAILED: publish reproducible but hashes to 4B6345363E6133CD1769677177276164C56F181A915A45617DE4DAC177FB414A, pin is 3E249136155B1594D42A1E64E853BA071175D95888079FBA23909E16A9FBFA3A. osx-arm64 failed the same way. Fix filed as BL-1329; re-run this check once it is Done.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. Run 43995901070 failed on win-x64: CI hash differs from pin; fix is BL-1329, then re-dispatch
