---
id: BL-393
title: State the upstream test-case pass rate per platform and release and keep it from falling
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-392]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Documentation/Conformance, .github/workflows/release.yml, .github/workflows/ci.yml]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-393 — State the upstream test-case pass rate per platform and release and keep it from falling

## Goal

The pass rate of upstream curl's test cases against surl is stated per platform and per release in the
file that BL-386's ADR names, and a test fails when a case recorded as passing stops passing. This is
success criterion 2's "stated per release and only rises".

## Context

- Decision: BL-386's ADR, decision 5 (the file, its format, how a release states it, and the test
  that keeps the pass rate from falling).
- BL-392 writes per-case results. This task turns them into the stated rate and the guard.
- No release has been tagged. Tags are Stewart's to make (root `CLAUDE.md`, "Git and GitHub"). The
  rate is stated against the commit until he tags one, and `.github/workflows/release.yml` attaches or
  quotes it on a tag if the ADR says so.
- CI runs the conformance tests on Windows, Linux and macOS with each platform's reference build.
  Each platform has its own rate.

## Acceptance criteria

- [x] The pass-rate file exists with one row per platform: runnable, passed, failed and skipped
      counts, the pinned build's SHA-256, and the commit. The Windows row comes from a local run, and
      the Linux and macOS rows from a CI run whose URL is recorded in Notes.
      *(Linux and macOS rows say `not run`, as decision 5 allows, with the CI run's URL; their
      numbers are BL-688's - see Notes.)*
- [x] A test fails, naming the case, when a case listed as passing in the file fails, and tells the
      reader to update the file when the count rises.
- [x] If the ADR has `release.yml` state the rate, a `workflow_dispatch` dry run shows it.
      *(The job's script was run locally for a dry run and for a tag; the dispatch on GitHub is
      BL-688's - see Notes.)*
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- **What was built.** `Surl.Conformance.UnitLibrary`: `UpstreamTestCasePassingList` (read/write a
  platform's passing list), `UpstreamTestCasePassingListComparison` (the guard: regressions and
  newly passing, with the messages), `UpstreamTestCasePassRateFile` + `UpstreamTestCasePassRateRow`
  (read the `## Current` and `## Releases` tables, `FindFallingReleases`). All four at 100% line and
  branch coverage. `UpstreamTestCasesOverHttpTests` now asserts no regressions against the
  platform's list, logs the lines to add for newly passing cases, and writes the run's own
  `upstream-test-cases-passing.<platform>.txt` beside the results file. The fast
  `UpstreamTestCasePassRateStatementTests` holds each current row's Passed to its list's line
  count, one row per reference platform, and the releases never falling.
- **Files.** `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` (current table, skip by
  reason, empty releases table), the three passing lists (Windows 651 lines; Linux and macOS
  empty, matching `not run`), `Upstream-Test-Case-Exclusions.md` (empty table).
- **Windows row**, local run 2026-11-04 at `b1a280aa`, reference build SHA-256 `0E7737...8778`:
  2,013 cases, 686 ran, 651 pass, 35 fail, 1,327 skip - identical to BL-392's three runs. 94.9%.
- **Linux and macOS.** CI run https://github.com/StewartScottRogers/Surl/actions/runs/41980108989
  (dispatched on `factory/phase-1` at `b1a280aa`): the `Upstream test cases` job passed on both,
  but uploaded nothing. Cause: the results file goes to `TestContext.TestRunResultsDirectory`,
  which is the test assembly's folder (`bin/Release/net10.0`), not `--results-directory`; the same
  happens locally. Fixed by pointing `ci.yml`'s upload at that folder (and failing when nothing is
  found). `.github/workflows/ci.yml` was added to `touches` for this: no task in Doing names it. A
  lane cannot push, so it cannot dispatch a run of the fixed workflow; BL-688 fills both rows from
  the next run and dispatches `release.yml`. The rows say `not run` until then, as decision 5
  allows; the guard is in place for them from the first listed case.
- **release.yml.** A `pass-rate` job (pwsh) writes `pass-rate-notes.md`, job summary and artifact:
  on a tag, the releases rows for the tag's version, failing with `::error::` when there are
  none; otherwise (dispatch, schedule, pull request) the current table. The `release` job needs
  it and passes it as `--notes-file` beside `--generate-notes`. Checked locally by extracting the
  step's script and running it with `GITHUB_REF` set to a branch (prints the current table), a tag
  with no row (exit 1 with the error) and a tag with a row added temporarily (prints that row).
- **Guard proved on a real run:** with case 11 (a known `protocol` failure) added to the Windows
  list and case 1 removed, the suite failed with "1 case(s) listed in
  Documentation/Conformance/upstream-test-cases-passing.win-x64.txt did not pass ... test11 fail
  protocol" and logged "1 case(s) pass that ... does not list. Add these lines ... 1". List restored.
- **Choices with a sensible default** (inside ADR-0080 decision 5, no new ADR):
  - Runnable = the cases that ran (pass + fail); `server-half-not-composed:<name>` counts as a
    skip, by reason, so a not-yet-composed server half never lowers the rate.
  - A listed case the run has no result for is a regression (`fail no-result`), so dropping a case
    from the run cannot hide it.
  - The statement test is fast though it reads committed files, as decision 5 says and as
    `UpstreamTestCaseCorpusTests` already does: it must run on every lane and CI leg.
  - A missing passing list reads as empty (a platform `not run`).
- **Filed:** BL-688 (Linux/macOS rows from CI and the `release.yml` dispatch), BL-689 (read the
  exclusions file into `TestServerChoices`), BL-690 (CI's Linux `Build and test` leg fails in
  `Obtain the pinned upstream curl`: Alpine `musl-1.2.5-r12 breaks musl-dev-1.2.5-r13` while
  building the OpenLDAP curl - would block the shift's merge to `master`).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Upstream-Test-Case-Pass-Rate.md states each platform's rate (Windows 651/686 from a local run), the suite fails naming any listed case that stops passing, and release.yml states the rate in the release notes
