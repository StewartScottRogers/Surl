---
id: BL-1121
title: Record the Linux and macOS upstream test-case pass rates from a dispatched CI run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1118, BL-1119, BL-1120]
touches: [Documentation/Conformance]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1121 — Record the Linux and macOS upstream test-case pass rates from a dispatched CI run

## Goal

The `linux-x64` and `osx-arm64` rows of `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md`
and their passing lists come from one CI run of a commit that contains BL-1118, BL-1119 and BL-1120.
That run is the first to include the http/2, http/3 and mail-TLS halves. Both rows date from
2026-11-06 (commit `8eb0301b`) today.

## Context

- CI ignores pushes to `factory/**` (`.github/workflows/ci.yml`), so dispatch it:
  `gh workflow run CI --ref factory/phase-1`. Then find the run with `gh run list --workflow CI`.
  BL-1105 worked the same way.
- The `Upstream test cases (ubuntu-latest)` and `(macos-latest)` jobs upload
  `upstream-test-case-results-linux-x64` and `-osx-arm64`. Download them with
  `gh run download <id> -n <name>`. Each holds the results file and the run's passing list.
- To raise a row, follow the steps in `Upstream-Test-Case-Pass-Rate.md` ("To raise a platform's
  count"):
  - copy each passing list over `Documentation/Conformance/upstream-test-cases-passing.<platform>.txt`;
  - update the row: commit, date, Runnable, Passed, Failed, Skipped, test-server-choice, pass rate;
  - update the `### Skipped, by reason` columns from the results file;
  - add a dated paragraph naming the run URL and which of 1700, 1701, 1702, 2403, 2500, 2503, 987,
    988 and 989 passed.

  A number may leave a list only together with an exclusions row (ADR-0080 decision 5).
- If a case fails, do not fix it here. File one task per cause (lanes may file extra tasks) with the
  case number, the failing section and its first differing line from the results file.
- This is a measurement with pinned upstream curl on CI, not a code change.

## Acceptance criteria

- [ ] Notes hold the CI run URL and its head commit, and `git merge-base --is-ancestor` shows the
      commits of BL-1118, BL-1119 and BL-1120 under it.
- [ ] The `linux-x64` and `osx-arm64` rows and their passing lists match that run's artifacts.
      `UpstreamTestCasePassRateStatementTests` passes.
- [ ] The skip table's Linux and macOS columns match the run's results files. Neither column has a
      `server-not-built:http/2` or `server-not-built:http/3` skip left.
- [ ] Every case among 1700, 1701, 1702, 2403, 2500, 2503 and 987 to 989 that failed on either leg
      has a filed task, named in Notes.

## Notes

## Log

- 2026-11-14: Created.
