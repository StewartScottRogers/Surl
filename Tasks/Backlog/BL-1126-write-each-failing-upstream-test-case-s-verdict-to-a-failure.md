---
id: BL-1126
title: Write each failing upstream test case's verdict to a failures file that CI uploads
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, .github/workflows/ci.yml]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1126 — Write each failing upstream test case's verdict to a failures file that CI uploads

## Goal

Each run of the upstream test-case suite writes `upstream-test-case-failures.<platform>.txt` beside
its results file: for every failing case, its number, the full verdict (first differing section and
line, expected and actual) and the server halves' notes. CI uploads it in the
`upstream-test-case-results-<platform>` artifact, so a Linux or macOS failure can be diagnosed
without rerunning it.

## Context

- Today the verdict only goes to `TestContext.WriteLine` in
  `Conformance/Surl.Conformance.UnitTests/UpstreamTestCasesAgainstSurlTests.cs` (the line
  `testContext.WriteLine($"{client.LogPrefix}test{number}: {verdict}...")`, and the timeout and
  exception lines near it). CI's job log for run 43629075978 (jobs 132551422516 and 132551422538)
  holds none of it, and the results file holds only `test<N> fail <section>`. So the causes of
  Linux/macOS-only failures 1072, 1073 (`errorcode`) and macOS-only 4001 (`errorcode`) cannot be
  read from the recorded run; the triage task that depends on this one needs this file.
- Write the failures file in the same folder and the same way the results file is written (find
  where `upstream-test-case-results.` is composed in the test class). One block per failing case, in
  test-number order, starting `test<N> fail <section>`; a timed-out case says `curl stopped after
  60 s`. Include curl's exit code and its stderr for `errorcode` and `stderr` failures.
- `.github/workflows/ci.yml`, job `Upstream test cases`, step that uploads
  `upstream-test-case-results-${{ matrix.upstream-curl-platform }}`: add
  `Conformance/Surl.Conformance.UnitTests/bin/Release/net10.0/upstream-test-case-failures.*.txt` to its
  `path:` list. Keep `if-no-files-found: error`; write the file (empty) even when nothing fails.
- Stand-in: not applicable. This is conformance tooling in the test project; no protocol server,
  store or event stream changes.
- The lane can check it locally: the Windows reference run takes about 75 s
  (`dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamTestCasesAgainstSurlTests"`)
  and today fails test 1452 (`timeout`), so the file has exactly one block.

## Acceptance criteria

- [ ] A local Windows run writes `upstream-test-case-failures.win-x64.txt` beside
      `upstream-test-case-results.win-x64.txt`, with one block per `fail` line of the results file
      (today: test 1452, `curl stopped after 60 s`).
- [ ] An `errorcode` block shows the expected and actual exit codes and curl's stderr; a fast test
      in `Surl.Conformance.UnitTests` pins the block format for a fail verdict and for a timeout.
- [ ] `.github/workflows/ci.yml`'s upload step lists the failures file's path.
- [ ] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast tests
      are green.

## Notes

## Log

- 2026-11-14: Created.
