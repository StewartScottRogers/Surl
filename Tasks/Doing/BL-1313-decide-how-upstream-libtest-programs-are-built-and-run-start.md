---
id: BL-1313
title: Decide how upstream libtest programs are built and run, starting with 2402, 2404, 2407 and 2502 (ADR-0173)
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-16
completed:
---
# BL-1313 — Decide how upstream libtest programs are built and run, starting with 2402, 2404, 2407 and 2502 (ADR-0173)


## Goal

An ADR decides how upstream's libtest programs are built, pinned and run, starting with cases 2402, 2404, 2407 and 2502, and files the tasks that do it.

## Context

ADR-0173 decision 7 leaves these four unrunnable. ADR-0080 skips 350 cases as `libtest`. Stewart's 2026-11-16 approval covers only the curl 8.21.0 build for the Debug cases; check whether libtest sources are in the same tarball (they are, under `tests/libtest`) and so need no further download.

## Acceptance criteria

- [ ] An ADR marked "Decided by Claude under Stewart's delegation" decides how the libtest binaries are built, pinned (by SHA-256 as kind `libtest` or by tree hash) and run by the conformance runner, with alternatives.
- [ ] It names which of the 350 libtest cases it will run first and which stay unrunnable and why.
- [ ] Follow-up tasks are filed with `task-board.ps1 new`; their IDs are in Notes.

## Notes

## Log

- 2026-11-16: Created by BL-1303 (ADR-0173).
- 2026-11-16: Backlog -> Doing.
