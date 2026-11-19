---
id: BL-1371
title: Replace the coverage exclusion on DiskConfigFileSource.OpenProcessStandardInput with a test
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-1371 — Replace the coverage exclusion on DiskConfigFileSource.OpenProcessStandardInput with a test

## Goal

`DiskConfigFileSource.OpenProcessStandardInput` is covered by a fast test instead of excluded, and
`Measure-CodeQuality.ps1 -Library Surl.Console` still reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (after BL-1346..BL-1370 merged in PR #106). Every gate is met
  in all 82 production assemblies. This is the only new `[ExcludeFromCodeCoverage]` since the previous audit.
- `Application/Surl.Console/DiskConfigFileSource.cs:23` -
  `[ExcludeFromCodeCoverage(Justification = "Opens the process's standard input; tests pass a stream instead.")]` on
  `internal static Stream OpenProcessStandardInput() => System.Console.OpenStandardInput();` (line 24). BL-1352 added
  this method and its exclusion (commit cd94a05e1). It gave the method group a distinct cached delegate, which closed
  the branch gap in `CommandLineRunner`'s constructor at line 102.
- A test can reach the member. `Console.OpenStandardInput()` returns a stream without reading from it on Windows,
  Linux and macOS. `Surl.Console` already has `InternalsVisibleTo Surl.Console.UnitTests`. The justification explains
  why tests pass a stream instead, not why a test cannot reach the member. An exclusion needs a reason the member
  cannot be reached, so this one is a coverage gap hidden by an attribute. The precedent BL-1352 cited,
  `LogFile.Open` (`LogFile.cs:23`), is different: it opens a file on disk and has integration tests. Nothing tests
  `OpenProcessStandardInput`.

## Acceptance criteria

- [x] `[ExcludeFromCodeCoverage]` and its `using System.Diagnostics.CodeAnalysis;` (if then unused) are removed from
      `DiskConfigFileSource.cs`; the method itself and `CommandLineRunner`'s use of it at line 102 are unchanged.
- [x] A named `Surl.Console.UnitTests` test (e.g. `DiskConfigFileSourceTests.OpenProcessStandardInput_ReturnsAReadableStream`)
      calls `DiskConfigFileSource.OpenProcessStandardInput()`, asserts the stream is not null and `CanRead`, and
      disposes it without reading. The test is not marked `Integration` or `OSCondition`.
- [x] `Measure-CodeQuality.ps1 -Library Surl.Console` reports `100 | 100` with 0 failing members, and its
      "Coverage exclusions in production code" section no longer lists `DiskConfigFileSource.cs`.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- Tests only (test-writer). Removing the attribute is the only production edit.
- If the test cannot pass on one platform (it should not happen), do not put the exclusion back. Instead, keep the
  exclusion with a comment directly above it naming that platform and the failure seen, and say so here.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. Removed the exclusion; added test; Surl.Console 100|100, 0 failing
