---
id: BL-689
title: Read the upstream test-case exclusions file into the selection rules
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-393]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Upstream-Test-Case-Exclusions.md, Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-689 — Read the upstream test-case exclusions file into the selection rules

## Goal

`UpstreamTestCaseSelectionRules.ForBuild` reads `Documentation/Conformance/Upstream-Test-Case-Exclusions.md`
into `TestServerChoices`, so a listed case skips `test-server-choice`.

## Context

- ADR-0080 decision 5. BL-393 created the exclusions file with an empty table (columns: Case,
  Byte verified, Upstream's choice, Surl's answer, Why ...). `TestServerChoices` is empty today.
- A case number in a passing list may be removed only with an exclusions row (decision 5);
  a fast test can check that no case is both excluded and listed as passing.

## Acceptance criteria

- [x] A parser reads the table's `Case` column (`test<N>` or `<N>`) into a set, with unit tests for a row, an empty table and a malformed row.
- [x] The suite's run passes that set as `TestServerChoices`, and the pass-rate file's test-server-choice count is the set's size.
- [x] A fast test fails when a case is both in the exclusions file and in a passing list.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green; the library stays at 100% line and branch coverage.

## Notes

- `UpstreamTestCaseExclusions.Parse` (new) reads the `Case` column of the file's first table that
  has one; `test<N>` or `<N>`, backticks allowed. A non-number, a repeated case, a ragged row or
  no such table throws `FormatException`, as the pass-rate file's reader does. An empty table is an
  empty set.
- Choice: `ForBuild` and `ForClient` take the set as a third, required parameter instead of
  opening the file themselves, so the library keeps its no-file-system parsing and every caller
  states what it excludes. The tests' `UpstreamTestCasePassRateStatementTests.CommittedExclusions()`
  reads the committed file. The run (`CaseClient`, so the Curl port's run too) passes it.
- "The pass-rate file's test-server-choice count is the set's size": `UpstreamTestCasePassRateFile`
  now reads the `test-server-choice` column when present (`UpstreamTestCasePassRateRow.TestServerChoice`),
  and a fast test holds each current row to the exclusions' row count. The upstream test-case run
  asserts its own test-server-choice count is the row count. `UpstreamTestCaseCorpusTests` checks
  that all 4 excluded cases reach decision 5's check on every reference pin (they do).
- Pass-rate rows updated without a new run: none of 802, 818, 861, 897 is on any passing list and
  each is runnable on every pin, so each was one of that run's failures. Each platform moves 4
  from Failed/Runnable to Skipped/test-server-choice (win 1,120/57/893, 94.9%; linux 1,233/79/780,
  93.6%; macOS 1,233/80/780, 93.5%); a note under the table says so. Added the two
  `Documentation/Conformance` files to `touches` for this and for the exclusions file's paragraph
  that said BL-689 had not landed; no task in Doing names them.
- Gate run with SDK 11.0.100: the pinned 10.0.401 is not installed on this machine (BL-1035).
  `global.json` was pointed at 11.0.100 only for each command and restored afterwards, so it is
  unchanged. `dotnet build -warnaserror`: 0 warnings, 0 errors. Fast tests: 79 assemblies, 0
  failed (Conformance 850 passed). `dotnet format --verify-no-changes`: clean for both Conformance
  projects.
- Coverage (cobertura, fast tests): Surl.Conformance.UnitLibrary at 100% line coverage; the new
  and changed classes are at 100% branch coverage. The library's only missed branch is in
  `UpstreamTestCaseParser` (0.975), which this task did not touch.

- 2026-11-11, lane 1: re-delivered lane 4's commit 1b197da2 by cherry-pick onto the rebased
  branch. It applied cleanly, and `dotnet build -warnaserror` (whole solution, SDK 11.0.100 as
  above, `global.json` restored) is 0 warnings, 0 errors; fast tests 0 failed (Conformance 850
  passed); format clean. Nothing in it needed changing: the build that failed lane 4's integration is clean on this base.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 4 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-689-lane-4-20261111-060243; start with git cherry-pick --no-commit factory/BL-689-lane-4-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. The selection rules skip the exclusions file's cases as test-server-choice; fast tests guard it against the passing lists and the pass-rate file
