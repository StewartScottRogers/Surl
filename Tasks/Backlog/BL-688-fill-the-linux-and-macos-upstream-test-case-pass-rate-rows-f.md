---
id: BL-688
title: Fill the Linux and macOS upstream test-case pass-rate rows from CI and show release.yml's dry run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-393]
touches: [Documentation/Conformance]
requirement: none
created: 2026-11-04
completed:
---
# BL-688 — Fill the Linux and macOS upstream test-case pass-rate rows from CI and show release.yml's dry run

## Goal

`Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` states the Linux and macOS rows from a
CI run, and a `workflow_dispatch` dry run of `.github/workflows/release.yml` shows the rate.

## Context

- ADR-0080 decision 5; BL-393 wrote the file, the guard and the `pass-rate` job of `release.yml`.
- CI run 41980108989 (commit `b1a280aa`) ran the suite on Linux and macOS and it passed, but the
  results file is written to the test assembly's folder, not `--results-directory`, so nothing was
  uploaded. BL-393 changed `ci.yml`'s upload path; a lane cannot push or dispatch a workflow on
  unpushed work, so this waits for BL-393 to reach GitHub.
- Run `gh workflow run ci.yml --ref <branch holding BL-393>`, download the
  `upstream-test-case-results-linux-x64` and `-osx-arm64` artifacts, copy each
  `upstream-test-cases-passing.<platform>.txt` over the committed (empty) one, and fill the row:
  runnable, passed, failed, skipped (and the skip-by-reason column), commit, date.
- Then `gh workflow run release.yml --ref <branch>` and read the `State the upstream test-case
  pass rate` job's summary.

## Acceptance criteria

- [ ] The `linux-x64` and `osx-arm64` rows hold numbers from one CI run whose URL is in Notes, and
      each passing list's line count equals its row's Passed (`UpstreamTestCasePassRateStatementTests` green).
- [ ] The CI artifact `upstream-test-case-results-<platform>` holds the results file and the passing list for both legs.
- [ ] A `workflow_dispatch` run of `release.yml` shows the current table in its `pass-rate` job summary; its URL is in Notes.

## Notes

## Log

- 2026-11-04: Created.
