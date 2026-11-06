---
id: BL-599
title: Split UpstreamCurlBuildFeatures.AddFromVersionLine and AddRenamedFeatures below complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-06
---
# BL-599 — Split UpstreamCurlBuildFeatures.AddFromVersionLine and AddRenamedFeatures below complexity 10

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Conformance.UnitLibrary` reports no failing member.

## Context

- Found by BL-578 on 2026-11-04: `Measure-CodeQuality.ps1` measures `UpstreamCurlBuildFeatures.AddFromVersionLine`
  (`UpstreamCurlBuildFeatures.cs:88`) and `AddRenamedFeatures` (`:110`) at cyclomatic complexity 12, above the
  threshold of 10, at 100% line and branch coverage. The build's `CA1502` passes them, so the script's IL-based
  count is what fails. Both landed with BL-389 (commit 22100843).
- Never raise the threshold; extract private methods with names that say what each part derives.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Conformance.UnitLibrary` reports 0 failing members.
- [x] `UpstreamCurlBuildFeaturesTests` pass unchanged, `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-11-06: By the time this ran the library had 12 members above complexity 10, not 2: also
  `UpstreamTestCasePassRateFile.ReadTable` (14), `UpstreamTestCaseVerifyChecker.Escape` and
  `CheckErrorCode`, `UpstreamTestCaseStripLine.ReadAt` and `TryParse`,
  `UpstreamTestCaseStripCondition.ReadToken` and `ParsePrimary`, `UpstreamTestCasePortComparison`'s
  constructor and `FormatFamilyTable`, and `WebSocketFrameUnmasking.ReadLengths` (12 each). None had a
  task, all sit inside this task's `touches`, and the Goal is 0 failing members, so all 12 were split
  here rather than filed separately.
- The script's complexity is the Cobertura attribute, which counts IL branches - including the null
  check that caches each non-capturing lambda - so a method with several `Count(row => ...)` or
  `Sum(x => ...)` lambdas measures high though `CA1502` passes it. Each was split into private methods
  named for what they derive (`AddFromLibraryWords`, `AddThreadedResolver`, `AddCryptoAndCertStatus`,
  `ReadTableCells`, `IsTableLine`, `EscapeCharacter`, `ExpectedErrorCode`, `AcceptedExitCodes`,
  `FromClearing`, `StartsArrayInterpolation`, `ReadSymbol`, `ReadWord`, `ParseParenthesised`,
  `ParseBoundMatch`, `CountFamilies`, `CountFamily`, `FormatTotalRow`, `ExtendedLengthSize`,
  `ReadPayloadLength`). No behaviour changed and no test changed.
- Result: 602 members, 0 failing, 100% line and branch coverage, worst CRAP 10.
- One full-solution test run saw
  `CoapFlowObserversTests.ServeAsync_DeleteFromAnotherFlow_Notifies404WithoutObserveAndRemovesTheObserver`
  fail once (it passed alone and in the next two full runs); filed as BL-931.

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Surl.Conformance.UnitLibrary measures 0 failing members: 12 methods split below complexity 10, behaviour unchanged
