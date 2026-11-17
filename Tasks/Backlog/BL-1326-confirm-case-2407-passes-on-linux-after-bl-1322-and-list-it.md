---
id: BL-1326
title: Confirm case 2407 passes on Linux after BL-1322 and list it as passing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1323, BL-1333, BL-1336]
touches: [Documentation/Conformance]
requirement: none
created: 2026-11-16
completed:
---
# BL-1326 — Confirm case 2407 passes on Linux after BL-1322 and list it as passing

## Goal

Case 2407 is listed as passing on linux-x64 once a Linux CI run confirms it.

## Context

BL-1323 found 2407's curl exit 6 ("Could not resolve host: -q") was runtests options reaching lib2405; BL-1322 stopped that. Not verifiable on Windows. Follow BL-1325.

## Acceptance criteria

- [ ] The linux-x64 failures file of a CI run after 2df5bfc9f does not list 2407, and Documentation/Conformance/upstream-test-cases-passing.linux-x64.txt does.

## Notes

- 2026-11-16: CI run 44009482486 (master 38930c7, contains 2df5bfc9f) still lists test2407 fail timeout on linux-x64; cause recorded by BL-1335, fix is BL-1336. Added BL-1336 to depends-on.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. No Linux CI run includes 2df5bfc9f yet (newest CI run, 2026-11-16T23:29Z, predates it); retry once a CI run on a later commit finishes
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. No CI run includes 2df5bfc9f yet (newest CI runs, master 6023b4b and merge 15ff9cb, do not contain it); retry after a CI run on a later commit finishes
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. CI run 44002670831 (contains 2df5bfc9f) lists test2407 fail timeout on linux-x64; cause is now BL-1333, retry after it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. CI run 44009482486 still lists test2407 fail timeout on linux-x64; waits on BL-1336
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Backlog. No CI run includes b2ff44414 (BL-1336 fix, committed 2026-11-17T10:42Z) yet; newest run 44065965398 started 10:33Z; retry after a CI run on a later commit finishes
