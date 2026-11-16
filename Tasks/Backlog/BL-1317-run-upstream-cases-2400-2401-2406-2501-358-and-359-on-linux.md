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
