---
id: BL-1351
title: Cover the pin and locator branches left in Surl.Conformance
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-1351 — Cover the pin and locator branches left in Surl.Conformance

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Conformance.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 100% line, 99.34% branch, 5 failing
  members, all branch-only, all in `Conformance/Surl.Conformance.UnitLibrary/`:
  - `CurlPortClientPins.ParseCiHashStable(JsonElement, int)` - `CurlPortClientPins.cs:91`, 16.67% branch (1 of 6): only
    the absent-`ciHashStable` case is seen; `true`, `false` and a non-boolean value are not.
  - `CurlPortClientLocator.VerifyFirstPresent(...)` - `CurlPortClientLocator.cs:123`, 92.86% branch; line 130 is 7 of 8
    (the `upstreamHashes`/`Matches`/`CiHashStable` condition).
  - `CurlPortClientLocator.Matches(string, string?)` - `CurlPortClientLocator.cs:142`, 50% branch: `expected is null` never true.
  - `UpstreamTestCaseLibtests.ProgramName(UpstreamTestCase, int)` - `UpstreamTestCaseLibtests.cs:26`, 80% branch; line 29
    is 8 of 10 (a case with no `<client><tool>` part, or one whose lines are all blank).
  - `<LocateLibtest>b__0(PinnedUpstreamCurlBuild)` - `UpstreamCurlLocator.cs:151`, 50% branch: a libtest pin for another
    platform (or a non-libtest pin) is never passed over.

## Acceptance criteria

- [x] Named `Surl.Conformance.UnitTests` tests parse `CurlPortBuilds.json` text with `ciHashStable` absent, `true`,
      `false` and a string, asserting the parsed flag or the refusal for each.
- [x] Named tests drive `CurlPortClientLocator` with a pin whose `Sha256` is null and with a `CiHashStable: false` pin
      matched only by the CI-accepted hash, so lines 130 and 142 see every outcome.
- [x] Named tests cover `UpstreamTestCaseLibtests.ProgramName` for a case with no tool part and one whose tool lines are
      all blank, and `UpstreamCurlLocator.LocateLibtest` with pins where a libtest pin for another platform comes first.
- [x] `Measure-CodeQuality.ps1 -Library Surl.Conformance.UnitLibrary` reports `100 | 100` with 0 failing members, and
      the fast tests are green.

## Notes

- Tests only (test-writer), over the existing file-access fakes; no process is started.
- Added tests to CurlPortClientPinsTests, CurlPortClientLocatorTests, UpstreamTestCaseLibtestsTests and UpstreamCurlLocatorTests; Measure-CodeQuality reports 0 failing members.
- Low priority: this library is the conformance harness, not a protocol server.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. Pin and locator branches covered; Surl.Conformance.UnitLibrary has 0 failing members
