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
completed:
---
# BL-1330 — Confirm build-curl-port is green on win-x64 and osx-arm64 after the BL-1329 re-pin

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. Run 43997573549 failed win-x64 and osx-arm64: hashes differ run to run; waits on BL-1331
