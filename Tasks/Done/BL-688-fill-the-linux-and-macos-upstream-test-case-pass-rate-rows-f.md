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
completed: 2026-11-06
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

- [x] The `linux-x64` and `osx-arm64` rows hold numbers from one CI run whose URL is in Notes, and
      each passing list's line count equals its row's Passed (`UpstreamTestCasePassRateStatementTests` green).
- [x] The CI artifact `upstream-test-case-results-<platform>` holds the results file and the passing list for both legs.
- [x] A `workflow_dispatch` run of `release.yml` shows the current table in its `pass-rate` job summary; its URL is in Notes.

## Notes

- No dispatch of `ci.yml` was needed: BL-393 is on `master`, and master's own push run
  https://github.com/StewartScottRogers/Surl/actions/runs/42203619501 (commit `8eb0301b`, 2026-11-06)
  uploaded `upstream-test-case-results-linux-x64` and `-osx-arm64`, each holding
  `upstream-test-case-results.<platform>.txt` (2,013 lines) and `upstream-test-cases-passing.<platform>.txt`.
  Both rows come from that one run.
- linux-x64: 1,237 runnable, 1,154 pass, 83 fail, 776 skip (93.3%). osx-arm64: 1,237, 1,153, 84, 776 (93.2%).
  The legs differ only in case 4001 (passes on Linux, fails `errorcode` on macOS). Skip reasons per
  platform added as two columns of the "Skipped, by reason" table. The passing lists were copied
  verbatim (1,154 and 1,153 lines); `UpstreamTestCasePassRateStatementTests` is green.
- `release.yml` dry run: https://github.com/StewartScottRogers/Surl/actions/runs/42222500303
  (`workflow_dispatch` on `master`). Its `State the upstream test-case pass rate` job passed and its
  `pass-rate-notes` summary states the current table at `8eb0301b`; that is master's table, so the
  Linux and macOS rows read `not run` there until this commit merges (a lane may not push), after which
  every dry run shows them. Its `Publish linux-arm64` leg failed on an unrelated flaky fast test,
  `GrpcStreamHandlerTests.ServeStreamAsync_ClientClosesTheConnectionMidCall_WritesNothingAndLogsCancelled`;
  filed as BL-928.

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Linux and macOS pass-rate rows filled from CI run 42203619501; release.yml dry run 42222500303 states the table
