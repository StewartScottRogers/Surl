---
id: BL-1338
title: Make the Linux passing list hold on CI: skip Debug-only cases where no Debug curl is installed, and fix or unlist libtests 2402, 2404 and 2502
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/upstream-test-cases-passing.linux-x64.txt, Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md, .github/workflows/release.yml]
requirement: none
created: 2026-11-16
completed:
---
# BL-1338 — Make the Linux passing list hold on CI: skip Debug-only cases where no Debug curl is installed, and fix or unlist libtests 2402, 2404 and 2502

## Goal

Merge PR #97's two red Linux jobs go green on the task's commit: "Conformance against the published linux-x64" and "Upstream test cases (ubuntu-latest)". Neither red job is caused by surl regressing.

## Context

Merge PR #97 (2026-11-17), frozen from `factory/phase-1`, failed `UpstreamTestCasesAgainstSurlTests.RunAsync_EveryCaseWithAComposedServerHalf_WritesOneResultLinePerCase` ("a passing case must keep passing") in two jobs:

1. **Release workflow, "Conformance against the published linux-x64"** (run 44012339002, job 133810649815). Nine listed cases were *skipped*, not failed: 358, 359, 2400, 2401, 2406 and 2501 (`debug-build`), and 2402, 2404 and 2502 (`libtest`). This job runs the suite against the published binary with only the reference curl. The Debug build and libtests are installed only in CI's "Upstream test cases" job (BL-1311 and BL-1316), so the one per-platform list (`upstream-test-cases-passing.linux-x64.txt`) cannot hold here.
   - Decide and implement one of these:
     - The check treats a listed case skipped for `debug-build` or `libtest` as not a regression *when the run has no Debug build or libtests binary*. This must stay strict when they are present, so a skip is still never counted as a pass in the stated rate.
     - The published-binary job installs the Debug build and libtests too.
     - The list splits by build.
   - Record the choice in ADR-0080 (or the ADR that added the Debug build, from BL-1303) as an amendment.
2. **CI, "Upstream test cases (ubuntu-latest)"** (run 44012338967, job 133802069974). With the Debug libtests installed, libtests 2402, 2404 and 2502 *failed* their `file` section (`test2402 fail file` and the same for 2404 and 2502). BL-1318 listed them as passing. Find out why the output file differs:
   - If surl is wrong, measured against pinned upstream curl 8.21.0 (ADR-0003), fix the server.
   - If the case cannot pass deterministically, take it off the list together with an exclusions row (the pass-rate file's rule), and correct the pass-rate rows.
   - The job's `upstream-test-case-results-linux-x64` artifact holds the results file and the differing section.

Lanes run on Windows, so CI on the task's commit is the check. Push, and read the two jobs on the shift branch's CI or on the next frozen PR.

## Acceptance criteria

- [ ] "Conformance against the published linux-x64" passes on a commit containing this task, with no listed case reported as not passing.
- [ ] "Upstream test cases (ubuntu-latest)" passes on a commit containing this task. 2402, 2404 and 2502 are either passing or unlisted with an exclusions row and a stated reason.
- [ ] The rule for Debug-only and libtest cases in a run without those binaries is written in the ADR amendment and in `Upstream-Test-Case-Pass-Rate.md`'s explanation. The stated pass rate still never counts a skip as a pass.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-16: Created from merge PR #97's two red Linux jobs.
