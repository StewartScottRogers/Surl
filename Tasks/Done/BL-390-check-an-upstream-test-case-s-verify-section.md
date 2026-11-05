---
id: BL-390
title: Check an upstream test case's verify section
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-388]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Documentation/Planning/Decisions/ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-390 — Check an upstream test case's verify section

## Goal

`Surl.Conformance.UnitLibrary` checks a case's `<verify>` section against what happened: curl's exit
code, stdout and stderr, the files it wrote, and what the server half recorded receiving. It returns
pass, or fail with the first differing section and a readable difference.

## Context

- Decision: BL-386's ADR, decision 4 (how `<strippart>` Perl substitutions are applied) and decision 2
  (what the server half records).
- FILEFORMAT.md at tag `curl-8_21_0`, `<verify>` (lines 674 to 811): `<errorcode>`, `<strip>` (lines
  removed by regex), `<strippart>` (Perl `s///` expressions), `<protocol>` and `<proxy>` with
  `nonewline` and `crlf`, `<stderr mode="text|warn">`, `<stdout mode="text" loadfile=...>`,
  `<file name=...>`, `<file1>` to `<file4>`, `<stripfile>`, `<upload>` and `<notexists>`.
  `mode="text"` compares with platform line endings.
- `<postcheck>` and `<valgrind>` are outside what this checker runs unless the ADR says otherwise.
- Inputs come from `UpstreamCurlRunResult` (exit code, stdout bytes, stderr text) and from the server
  half's record. The checker itself runs no process.

## Acceptance criteria

- [x] Named tests pass and fail each verify section on inline cases: `<errorcode>` match and mismatch,
      `<protocol>` with `<strip>` and `<strippart>`, `nonewline`, `crlf="yes"` and `crlf="headers"`,
      `mode="text"`, `<file name>`, `<upload>` and `<notexists>`.
- [x] Each `<strippart>` expression that occurs in the imported corpus is either applied or listed as
      unsupported with its test numbers. A test enumerates them, and Notes give the count.
- [x] A failure message names the section and shows the first differing line with its escaped bytes
      (NFR-020's escaping).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

- touches: added ADR-0080's file, because decision 4's strip subset had to be amended to match
  the corpus (ASCII `\d`/`\w`/`\s`, `or $_ = ''`, punctuation escapes in replacements, Perl truth
  after `<stripfile>`, operator binding). No task in Doing names it.
- Count: the imported corpus holds 104 distinct `<strip>`/`<strippart>`/`<stripfile>` lines in 262
  cases; 103 are applied and 1 is unsupported - test 1286's `map`/`sort`/`split` line
  (`UpstreamTestCaseCorpusTests.IsSupported_EveryStripLineOfEveryImportedCase_IsInterpretedOrListedWithItsCases`).
- Checks run in runtests' `singletest_check` order (stdout, stderr, protocol, data/datacheck,
  upload, proxy, file to file4, errorcode, notexists); the first differing section is reported.
- `<stderr mode="warn">` always passes, because runtests' `join` leaves both sides empty.
- `<postcheck>` and `<valgrind>` are not run, as the task's context says; the ADR does not add them.
- A strip line outside the subset makes `Check` throw `NotSupportedException`: the selector skips
  such a case (`strippart-unsupported`) first, so reaching it is a caller error.
- Coverage: `Surl.Conformance.UnitLibrary` 100% line and 100% branch. `Measure-CodeQuality.ps1`'s
  IL count puts six new methods at 12, as it already puts two in `UpstreamCurlBuildFeatures`; the
  build's CA1502 gate (`CodeMetricsConfig.txt`) passes them.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Verify checker built: every verify section, strip subset applied to 103 of 104 corpus lines; 100% coverage, fast tests green
