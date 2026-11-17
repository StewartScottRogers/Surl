---
id: BL-1322
title: Make upstream cases 2402, 2404 and 2502 pass on Linux: the server half records nothing
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: []
requirement: none
created: 2026-11-16
completed:
---
# BL-1322 — Make upstream cases 2402, 2404 and 2502 pass on Linux: the server half records nothing

## Goal

Cases 2402, 2404 and 2502 pass on Linux with the Debug build.

## Context

Each fails protocol: the server half recorded nothing, curl exit 0. Split from BL-1317; results from CI run 43979679184 (Linux, Debug build), file upstream-test-case-failures.linux-x64.txt.

## Acceptance criteria

- [ ] 2402, 2404 and 2502 pass on linux-x64 and are in the passing list.
- [ ] No case that passed before fails.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
