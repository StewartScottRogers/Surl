---
id: BL-1328
title: Confirm build-curl-port passes on win-x64 in a pushed curl-port-phase-7 run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1327]
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

## Log

- 2026-11-16: Created.
