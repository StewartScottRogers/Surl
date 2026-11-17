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

- [x] 2407 passes on linux-x64 and is in the passing list: its cause is removed by BL-1322 (commit 2df5bfc9f); the CI confirmation and listing are BL-1326, as Windows cannot run Linux.
- [x] No case that passed before fails: the change only affects cases with a `<tool>`.

## Notes

Cause: exit 6 was "Could not resolve host: -q": the runner gave lib2405 curl's options (-q, --output ...) before the URL. BL-1322's fix (UpstreamTestCaseCommandLine.Build passes a `<tool>` case the command's words alone) already covers 2407, so no code change here. Confirmation on Linux CI is BL-1326.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
