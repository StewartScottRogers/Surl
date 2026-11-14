---
id: BL-1149
title: Make upstream test cases 956, 957 and 958 pass on Linux and macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance, Documentation/Conformance]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1149 — Make upstream test cases 956, 957 and 958 pass on Linux and macOS

## Goal

Cases 956, 957 and 958 of upstream curl's tests (SMTP) pass on `linux-x64` and `osx-arm64` against Surl's
SMTP server half, and are in both passing lists.

## Context

- BL-1127 named 955-958; 955 now passes, 956-958 still fail in CI run 43650373643 (head `c3011bee`), found by BL-1142.
- 956: `fail protocol: line 4 differs: expected "QUIT\r\n", got "DATA\r\n"`, curl exit 0.
- 957 and 958: `fail errorcode: curl exited 0, expected 8`.
- All three show one SMTP exchange opened and ended by the server. The full blocks are in the CI artifact
  `upstream-test-case-results-linux-x64` (`upstream-test-case-failures.linux-x64.txt`); re-run via
  `gh workflow run CI --ref factory/phase-1`. Windows passes them? Check: they are on `win-x64`'s list or skipped there.
- Pinned upstream curl is the only oracle (ADR-0003); measure with `Record-CurlExchange.ps1`.

## Acceptance criteria

- [ ] Cause of each of 956, 957 and 958 found and recorded under Notes, with bytes measured from pinned upstream curl.
- [ ] The three cases pass in a CI run on Linux and macOS, and their numbers are in both
      `upstream-test-cases-passing.<platform>.txt` lists with the rows in `Upstream-Test-Case-Pass-Rate.md` updated.
- [ ] `UpstreamTestCasePassRateStatementTests` passes.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
