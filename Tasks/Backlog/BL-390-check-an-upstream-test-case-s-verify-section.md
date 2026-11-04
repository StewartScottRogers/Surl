---
id: BL-390
title: Check an upstream test case's verify section
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-388]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
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

- [ ] Named tests pass and fail each verify section on inline cases: `<errorcode>` match and mismatch,
      `<protocol>` with `<strip>` and `<strippart>`, `nonewline`, `crlf="yes"` and `crlf="headers"`,
      `mode="text"`, `<file name>`, `<upload>` and `<notexists>`.
- [ ] Each `<strippart>` expression that occurs in the imported corpus is either applied or listed as
      unsupported with its test numbers. A test enumerates them, and Notes give the count.
- [ ] A failure message names the section and shows the first differing line with its escaped bytes
      (NFR-020's escaping).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
