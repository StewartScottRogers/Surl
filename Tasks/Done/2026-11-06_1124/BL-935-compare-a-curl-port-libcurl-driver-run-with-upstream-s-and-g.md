---
id: BL-935
title: Compare a Curl port libcurl driver run with upstream's and give its Phase 7 verdict in Surl.Conformance
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-934]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: FR-057
created: 2026-11-06
completed: 2026-11-06
---
# BL-935 — Compare a Curl port libcurl driver run with upstream's and give its Phase 7 verdict in Surl.Conformance

## Goal

`Surl.Conformance` judges one Phase 7 libcurl row: a `CurlPortLibcurlCase` (a step list of a
pinned-libcurl test, `{surl}` standing for the run's authority) is run by upstream's driver and the port's
twin, each run kept as a `CurlPortLibcurlRun`, and `CurlPortLibcurlComparison.Compare` normalises both
reports and `surl`'s notes (`CurlPortLibcurlReportNormaliser`) and returns ADR-0089 decision 5's verdict.

## Context

- Third of the five tasks BL-597 ("Drive the Curl port's managed Libcurl through the WebSocket and RTSP
  libcurl scripts beside the pinned upstream libcurl") was split into on 2026-11-06 after it timed out
  at 120 minutes. Needs BL-934 (`CurlPortLibcurlCase` refers to `LibcurlWebSocketScript`). Requirement
  FR-057.
- Rules: ADR-0089 decision 4 (what is compared; its Amendment 3 for `surl`'s notes read settled and in
  exchange order) and decision 5 (verdicts: upstream's run failing the mirrored assertions or not exiting 0
  is a Surl defect and the port is not judged; a port difference is a port defect). What varies between
  two correct runs is masked, never compared: `surl`'s port in URLs, a session ID `surl` drew (not one the
  script set), a send's call count, and in interleaved RTP/RTCP frames the SSRCs and timestamps but not
  the body (the normaliser's tests name each).
- The work is written. Take it from the stash `darkfactory BL-597 20261106-033059` in the main checkout
  (`Z:\repos\Surl`; find its index N with `git stash list`, matching the message; never pop, apply or
  drop it). None of these files has moved on the branch since the stash's base `6b47feae`.
  - Tracked: `git diff "stash@{N}^" "stash@{N}" -- Conformance/Surl.Conformance.UnitLibrary/CurlPortComparison.cs | git apply`
    (`AddTextDifference` and `AddBytesDifference` become `internal` so the libcurl comparison reuses them).
  - Untracked, each with `git checkout "stash@{N}^3" -- <path>`: in
    `Conformance/Surl.Conformance.UnitLibrary`, `CurlPortLibcurlCase.cs`, `CurlPortLibcurlRun.cs`,
    `CurlPortLibcurlComparison.cs`, `CurlPortLibcurlReportNormaliser.cs`; in
    `Conformance/Surl.Conformance.UnitTests`, `CurlPortLibcurlCaseTests.cs`,
    `CurlPortLibcurlReportNormaliserTests.cs`, `CurlPortLibcurlComparisonTests.cs`.
- The port's output is measured, never an expected value (ADR-0003 decision 6).
- Stand-in: not applicable; test tooling.

## Acceptance criteria

- [x] `CurlPortLibcurlComparisonTests` passes, including `Compare_SameReportsNotesAndFiles_IsAgreement`,
      `Compare_UpstreamFailsTheMirroredAssertions_IsASurlDefectAndThePortIsNotJudged`,
      `Compare_PortReportDiffers_IsAPortDefectNamingTheFirstDifferingLine` and
      `Compare_StoredFileDiffers_IsAPortDefectOnThatFile`.
- [x] `CurlPortLibcurlReportNormaliserTests` and `CurlPortLibcurlCaseTests` pass, including
      `NormaliseLine_SessionTheScriptSet_IsKept` and
      `NormaliseLine_InterleavedFrames_ZeroesTheirRandomFieldsButNotTheBody`.
- [x] `dotnet build Conformance/Surl.Conformance.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Surl.Conformance.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Took the work from the stash `darkfactory BL-597 20261106-033059` (`90c03679`, found through
  `git reflog show refs/stash`): the `CurlPortComparison.cs` diff applied, and the seven untracked files
  checked out from its third parent. The stash itself was left alone.
- Choice: `CurlPortLibcurlComparisonTests` used `CurlPortClientLocatorTests.LibraryPin`, which needs
  `CurlPortClientKind`. That belongs to BL-942 (pinning the port's libcurl). The comparison never reads
  the pin's kind, so the test uses the existing `CurlPortClientLocatorTests.Pin` with the library's path
  instead, and BL-942 stays separate.
- Choice: the stash's four `[GeneratedRegex]` patterns became `static readonly Regex` fields, the form
  `CurlPortResultNormaliser` already uses. `Measure-CodeQuality.ps1` counts the source-generated regex
  code (`obj\...\RegexGenerator.g.cs`) as library members, which held the library at 96.9% line and
  94.4% branch coverage. With the change it is 100% / 100% with 0 failing members, worst CRAP 10.
- Results: `Surl.Conformance.UnitTests` 814 passed. `dotnet build -warnaserror` is clean. All 79 fast
  test projects are green.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. CurlPortLibcurlComparison.Compare normalises a Curl port libcurl driver run beside upstream's and gives ADR-0089 decision 5's verdict
