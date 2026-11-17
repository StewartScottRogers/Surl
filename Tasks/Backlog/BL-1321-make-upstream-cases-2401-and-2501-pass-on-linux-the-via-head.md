---
id: BL-1321
title: Make upstream cases 2401 and 2501 pass on Linux: the Via header nghttpx adds differs
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: []
requirement: none
created: 2026-11-16
completed:
---
# BL-1321 — Make upstream cases 2401 and 2501 pass on Linux: the Via header nghttpx adds differs

## Goal

Cases 2401 and 2501 pass on Linux with the Debug build.

## Context

2401 differs at protocol line 8: expected 'Via: 2 nghttpx', got 'Via: 1.1 nghttpx'. 2501 differs at line 7: expected 'Via: 3 nghttpx', got 'X-Forwarded-Proto: https'. Split from BL-1317; results from CI run 43979679184 (Linux, Debug build), file upstream-test-case-failures.linux-x64.txt.

## Acceptance criteria

- [ ] 2401 and 2501 pass on linux-x64 and are in upstream-test-cases-passing.linux-x64.txt.
- [ ] No case that passed before fails.

## Notes

## Log

- 2026-11-16: Created.
