---
id: BL-1323
title: Make upstream case 2407 pass on Linux: curl exits 6, expected 0
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: []
requirement: none
created: 2026-11-16
completed:
---
# BL-1323 — Make upstream case 2407 pass on Linux: curl exits 6, expected 0

## Goal

Case 2407 passes on Linux with the Debug build.

## Context

2407 fails errorcode: curl exited 6, expected 0. Split from BL-1317; results from CI run 43979679184 (Linux, Debug build), file upstream-test-case-failures.linux-x64.txt.

## Acceptance criteria

- [ ] 2407 passes on linux-x64 and is in the passing list.
- [ ] No case that passed before fails.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
