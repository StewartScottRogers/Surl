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
completed: 2026-11-16
---
# BL-1318 — Record the libtest 2402, 2404, 2407 and 2502 verdicts from a Linux run in Upstream-Test-Case-Pass-Rate.md (ADR-0174)

## Goal

The pass-rate file states the verdict of 2402, 2404, 2407 and 2502 as the runner of BL-1316 measured them on Linux with the pinned Debug build and libtests binary.

## Context

BL-1316 added the runner path; this machine (Windows) cannot run it. Needs the `Integration` upstream test-case run on Linux with `/opt/upstream-curl/8.21.0-debug/libtest/libtests` in place. A failing case is recorded as a failure with its reason, never excluded (ADR-0174 decision 6).

## Acceptance criteria

- [x] Upstream-Test-Case-Pass-Rate.md lists the four cases and the Linux libtest skip count drops by four.

## Notes

BL-1317 had already recorded the Linux run (CI 43979679184, commit 6023b4b3): the four cases ran and are among Linux's 6 failures (2402/2404/2502 record nothing, BL-1322; 2407 curl exit 6, BL-1323), and the skip table already shows the lower Linux libtest/debug-build counts. This task corrected the stale paragraph that still said the four stay skipped. Pass-rate statement tests pass (5/5).

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Blocked. Needs a Linux CI 'Upstream test cases' run with the BL-1316 runner and the Debug libtests binary; this Windows lane cannot run it. Re-open after a CI run on a commit containing BL-1316 uploads results.
- 2026-11-16: Blocked -> Backlog. Unblocked: BL-1316 now Done
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Blocked. Stewart: no CI run has the BL-1316 runner, and ci.yml runs only non-Integration tests without the Debug libtests binary; run the Integration upstream test cases on Linux with /opt/upstream-curl/8.21.0-debug/libtest/libtests and send the 2402/2404/2407/2502 verdicts
- 2026-11-16: Blocked -> Backlog. Unblocked: PR #94 merged to master (6023b4b38) with curl-port-phase-7.yml, the BL-1311 Debug build and the BL-1316 runner; master's CI now runs on a commit containing them.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Pass-rate file states the Linux verdicts of libtest cases 2402, 2404, 2407 and 2502 as failures with reasons
