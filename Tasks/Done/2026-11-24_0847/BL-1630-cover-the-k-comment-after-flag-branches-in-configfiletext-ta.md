---
id: BL-1630
title: Cover the -K comment-after-flag branches in ConfigFileText.TakesNoValue
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1630 — Cover the -K comment-after-flag branches in ConfigFileText.TakesNoValue

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Cli.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Surl.Cli.UnitLibrary 100% line,
  99.88% branch, 2 failing members. Both come from commit 44551a82d ("read # after a value-less flag in a -K file as a
  comment").
- `ConfigFileText.TakesNoValue(string)`, `Application/Surl.Cli.UnitLibrary/ConfigFileText.cs:127`, 87.5% branch. Line 135
  (`optionWord.Length > 1 && ...`) is 1 of 2: no test gives a short-option word that is a bare `-` (length 1) followed
  by ` #`.
- The lambda inside it (`<TakesNoValue>b__13_0(char)`), `ConfigFileText.cs:136`, 50% branch:
  `CommandLineOptions.TryFindShort(letter, ...) && option.Kind == CommandLineOptionKind.Flag` never sees one of its two
  outcomes. It needs a short-option cluster whose letter is unknown, or is a known option that takes a value, followed by
  ` #`, so the `#` is read as part of the value.

## Acceptance criteria

- [x] A named `Surl.Cli.UnitTests` test reads a `-K` line whose option word is a bare `-` followed by ` # text`, and
      asserts how the line is read.
- [x] A named test reads a `-K` line whose short-option cluster contains a letter that is not a value-less flag
      (unknown, or one taking a value) followed by ` # text`, and asserts that `#` is not taken as a comment.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Cli.UnitLibrary` reports `100 | 100` with 0
      failing members, and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Tests only (test-writer). No production change and no `[ExcludeFromCodeCoverage]`.
- The expected readings follow upstream curl's `-K` parsing. If a test exposes a reading that differs from upstream
  curl 8.21.0, file it as a new task rather than changing the parser here.


- 2026-11-16: Dispatched curl-port-phase-7 on factory/phase-1 (run 43982591865, workflow with macos-latest; master still has macos-14). osx-arm64 published twice with the same hash E616A59F...55BB and is pinned at 74f3f45. win-x64 CI hash still differs from the local pin (see earlier note). All entries now at 74f3f45; CurlPort pin tests pass.
- Added Parse_HashAfterANonFlagShortWord_IsTakenAsTheValueAndRefused (bare dash, unknown short letter, unknown long name, valued long name); the missing branches were the unknown-letter and long-name outcomes. Library now 100 | 100, 0 failing.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. Tests added; Surl.Cli.UnitLibrary 100/100
