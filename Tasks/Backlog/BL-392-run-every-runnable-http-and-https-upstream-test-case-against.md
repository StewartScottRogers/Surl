---
id: BL-392
title: Run every runnable http and https upstream test case against surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-389, BL-390, BL-391]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests, .github/workflows/ci.yml]
requirement: none
created: 2026-11-04
completed:
---
# BL-392 — Run every runnable http and https upstream test case against surl

## Goal

An integration test in `Surl.Conformance.UnitTests` runs every runnable `http` and `https` upstream
test case. For each case it starts surl's server half, runs the pinned reference build with the
case's command line as runtests would build it, checks the verify section, and writes one result line
per case: test number, pass, fail or skip, and the reason.

## Context

- Decision: BL-386's ADR, decisions 5 and 6 (results format, and where the suite runs).
- Pieces:
  - the parser (BL-388);
  - the selection (BL-389);
  - the checker (BL-390);
  - the HTTP server half (BL-391);
  - `PinnedUpstreamCurl` and `UpstreamCurlRunner` (pinned builds only, ADR-0003).
- How runtests builds the command (`tests/runner.pm` at tag `curl-8_21_0`): `-q` first unless
  `option="no-q"` (line 975), `--output` unless the case verifies stdout or says `no-output` (lines
  829 to 832), `--include` unless `no-include` (line 868), and `--trace-ascii` into the log directory
  (line 877). `<file>` and `<stdin>` are prepared before the run. `%LOGDIR` is a fresh temporary
  directory per case.
- The test is `[TestCategory("Integration")]`, and is inconclusive where no pinned build is installed,
  as the existing conformance tests are.
- If the ADR runs the suite in CI, add the step to `.github/workflows/ci.yml` within the ADR's time
  budget.

## Acceptance criteria

- [ ] Running `dotnet test Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamTestCases"`
      (or the class the ADR names) on Windows runs every runnable `http` and `https` case. It writes
      the results file the ADR names, and Notes record the pass, fail and skip counts.
- [ ] A case that hangs is ended by a per-case timeout and recorded as a failure with reason
      `timeout`. The run never hangs.
- [ ] Each failing case is either fixed in surl within this task, if it is a one-line defect, or
      grouped by cause into new tasks filed with `task-planner`, with their IDs in Notes. This task
      does not widen into fixing them all.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
