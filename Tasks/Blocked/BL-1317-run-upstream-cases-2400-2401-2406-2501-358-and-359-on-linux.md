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
completed:
---
# BL-1317 — Run upstream cases 2400, 2401, 2406, 2501, 358 and 359 on Linux with the Debug build and list the passes

## Goal

On `linux-x64`, the upstream test cases 2400, 2401, 2406, 2501, 358 and 359 have been run with the pinned Debug build and each passes or is a recorded finding.

## Context

Split from BL-1311, which made the suite choose the Debug build for these six cases (`UpstreamTestCaseDebugBuildCases`, `UpstreamCurlLocator.LocateDebug`). The run needs the Debug curl at `/opt/upstream-curl/8.21.0-debug/curl`, which only the CI `Upstream test cases` job on Linux builds (`.github/actions/upstream-curl`); this lane's machine is Windows with no Debug build. ADR-0173 decisions 6 and 7. Take the results from that job's uploaded results and failures files.

## Acceptance criteria

- [ ] 2400, 2401, 2406, 2501, 358 and 359 ran on Linux; each passes, or has a task filed for its first differing section.
- [ ] Passing cases are added to `upstream-test-cases-passing.linux-x64.txt` and the Linux row of `Upstream-Test-Case-Pass-Rate.md` is raised to match.
- [ ] No case that passed before fails.

## Notes

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
