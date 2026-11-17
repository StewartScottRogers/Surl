---
id: BL-1333
title: Find why case 2407 times out on Linux after BL-1322
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Documentation/Conformance]
requirement: none
created: 2026-11-16
completed:
---
# BL-1333 — Find why case 2407 times out on Linux after BL-1322

## Goal

Case 2407's Linux outcome is explained, and it passes or has a recorded cause.

## Context

BL-1326 found that CI run 44002670831 (commit 0cd51f95a, contains BL-1322's 2df5bfc9f) lists `test2407 fail timeout` in upstream-test-case-failures.linux-x64.txt. BL-1323's "Could not resolve host: -q" exit 6 is gone, but the case now times out. Start from that run's artifact `upstream-test-case-results-linux-x64`.

## Acceptance criteria

- [ ] The cause of the 2407 timeout on Linux is recorded in the family file under Documentation/Conformance, with a fix task filed if it is a Surl defect.

## Notes

## Log
