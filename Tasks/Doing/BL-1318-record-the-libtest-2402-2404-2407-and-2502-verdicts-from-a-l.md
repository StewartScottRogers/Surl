---
id: BL-1318
title: Record the libtest 2402, 2404, 2407 and 2502 verdicts from a Linux run in Upstream-Test-Case-Pass-Rate.md (ADR-0174)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1316]
touches: [Documentation/Conformance]
requirement: none
created: 2026-11-16
completed:
---
# BL-1318 — Record the libtest 2402, 2404, 2407 and 2502 verdicts from a Linux run in Upstream-Test-Case-Pass-Rate.md (ADR-0174)

## Goal

The pass-rate file states the verdict of 2402, 2404, 2407 and 2502 as the runner of BL-1316 measured them on Linux with the pinned Debug build and libtests binary.

## Context

BL-1316 added the runner path; this machine (Windows) cannot run it. Needs the `Integration` upstream test-case run on Linux with `/opt/upstream-curl/8.21.0-debug/libtest/libtests` in place. A failing case is recorded as a failure with its reason, never excluded (ADR-0174 decision 6).

## Acceptance criteria

- [ ] Upstream-Test-Case-Pass-Rate.md lists the four cases and the Linux libtest skip count drops by four.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
