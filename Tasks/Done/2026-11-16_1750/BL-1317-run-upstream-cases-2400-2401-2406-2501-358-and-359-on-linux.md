---
id: BL-1317
title: Run upstream cases 2400, 2401, 2406, 2501, 358 and 359 on Linux with the Debug build and list the passes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1311]
touches: [Documentation/Conformance]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1317 — Run upstream cases 2400, 2401, 2406, 2501, 358 and 359 on Linux with the Debug build and list the passes

## Goal

On `linux-x64`, the upstream test cases 2400, 2401, 2406, 2501, 358 and 359 have been run with the pinned Debug build and each passes or is a recorded finding.

## Context

Split from BL-1311, which made the suite choose the Debug build for these six cases (`UpstreamTestCaseDebugBuildCases`, `UpstreamCurlLocator.LocateDebug`). The run needs the Debug curl at `/opt/upstream-curl/8.21.0-debug/curl`, which only the CI `Upstream test cases` job on Linux builds (`.github/actions/upstream-curl`); this lane's machine is Windows with no Debug build. ADR-0173 decisions 6 and 7. Take the results from that job's uploaded results and failures files.

## Acceptance criteria

- [x] 2400, 2401, 2406, 2501, 358 and 359 ran on Linux; each passes, or has a task filed for its first differing section.
- [x] Passing cases are added to `upstream-test-cases-passing.linux-x64.txt` and the Linux row of `Upstream-Test-Case-Pass-Rate.md` is raised to match.
- [x] No case that passed before fails.

## Notes

Results from CI run 43979679184 (master 6023b4b3, Linux Debug build). Pass: 2400, 2406, 358, 359. Failures filed: 2401 and 2501 as BL-1321; 2402, 2404 and 2502 (newly runnable) as BL-1322; 2407 as BL-1323. No earlier pass regressed.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Blocked. Needs a Linux CI 'Upstream test cases' run that includes BL-1311's Debug-build selection; latest master run (43902295347) still skips all six as debug-build, and this Windows lane has no Debug curl. Re-open once a CI run after BL-1311 merges uploads results.
- 2026-11-16: Blocked -> Backlog. Unblocked: BL-1311 now Done
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Blocked. Needs a Linux CI 'Upstream test cases' run containing BL-1311 (31968b258); latest runs 43902295347 (master) and 43906686682 (phase-1) predate it, and this Windows lane has no Debug curl. Re-open after the next CI run on a commit with BL-1311 uploads results.
- 2026-11-16: Blocked -> Backlog. Unblocked: BL-1311 now Done
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Blocked. Stewart: no Linux CI 'Upstream test cases' run yet includes BL-1311 (31968b258); neither master af199cb5b nor phase-1 a06ae8611 contains it, and this Windows lane has no Debug curl - re-run after the next CI on a commit with it.
- 2026-11-16: Blocked -> Backlog. Unblocked: PR #94 merged to master (6023b4b38) with curl-port-phase-7.yml, the BL-1311 Debug build and the BL-1316 runner; master's CI now runs on a commit containing them.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Ran on Linux CI: 4 pass, 2 filed as BL-1321
