---
id: BL-578
title: Run one command line through pinned upstream curl and the pinned Curl port against the same surl and compare them
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-406]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-578 — Run one command line through pinned upstream curl and the pinned Curl port against the same surl and compare them

## Goal

`Surl.Conformance.UnitLibrary` can take one Phase 7 case and produce a verdict from BL-576's ADR decision 5.
The case is a command line, the `surl` options and content, and the upstream build the case uses. The library
runs the case through the pinned upstream build and the pinned Curl port, each against its own fresh `surl`,
and normalises both results as the ADR's decision 4 lists. It then returns either an agreement or a finding
in the ADR's decision 6 form. A test-side helper gives every Phase 7 test class one call that does this.

## Context

- Decisions: BL-576's ADR, decisions 2 to 6. Use the type names the ADR uses. Planning names:
  `CurlPortMeasurementCase`, `CurlPortComparison`, `CurlPortFinding`, `CurlPortVerdict`,
  `CurlPortResultNormaliser`.
- The two runners stay separate types (ADR-0083 decision 2). The upstream side is `UpstreamCurlLocator` and
  `UpstreamCurlRunner` (`LocateForProtocol` picks the build). The port side is BL-406's
  `CurlPortClientLocator` and its runner. The comparison takes the two run results. It never takes a path,
  so neither runner can be handed the other's binary.
- `Conformance/Surl.Conformance.UnitTests`: `PinnedUpstreamCurl` shows the test-side helper shape, and
  `SurlOnLoopback` starts a live `surl`. Add the Phase 7 helper beside them (planning name
  `PinnedCurlPortBesideUpstreamCurl`). When the upstream build or the port's binary is missing, it marks the
  test Inconclusive with the missing file's name.
- The port's own `DifferentialOutputNormaliser` and `DifferentialComparer` (read with
  `git -C Z:\repos\Curl show origin/master:Curl.Conformance.Differential.UnitLibrary/<file>`) may be copied
  as code (ADR-0003 decision 7). Rename them to this ADR's names and write their tests from the ADR's rules.
- Client request bytes come from BL-579. This task leaves a place in the comparison for them and compares
  everything else the ADR lists.
- The library's `CLAUDE.md` says "never the Curl port". BL-406 may already qualify that line. Make sure the
  file states that the port runs here only as the thing measured (ADR-0003 decision 6, ADR-0083), and
  describe the new types.

## Acceptance criteria

- [x] Named unit tests cover the comparison with no process and no network. There is one test per verdict
      the ADR names, and one per normalisation rule in its decision 4. Each normalisation test shows that two
      results differing only in that field agree, and that a difference elsewhere is still a finding.
- [x] A unit test shows that a finding carries every field the ADR's decision 6 lists: upstream build
      SHA-256, port commit and SHA-256, Surl commit, command line and both results.
- [x] One `[TestCategory("Integration")]` smoke test in `Conformance/Surl.Conformance.UnitTests` measures
      `curl http://127.0.0.1:<port>/hello.txt` against `surl http://`. With both pinned binaries present on
      Windows it reaches a verdict, and without them it is Inconclusive.
- [x] `Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md` describes the Phase 7 types and the port's role.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Conformance.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

- Plan, as built: `CurlPortMeasurementCase`, `CurlPortSurlSide`, `CurlPortMeasuredRun`, `CurlPortResultNormaliser`,
  `CurlPortComparison`, `CurlPortVerdict` + `CurlPortVerdictKind`, `CurlPortFinding`, `CurlPortDifference` in
  `Surl.Conformance.UnitLibrary`; `PinnedCurlPortBesideUpstreamCurl` and its smoke test in the tests. The port's
  `DifferentialOutputNormaliser`/`DifferentialComparer` were read but not copied: their rules (debug-build lines,
  cwd, cookie jars) are the port's own differential harness's, not ADR-0089 decision 4's, so the normaliser was
  written from the ADR's table instead.
- Choices with a sensible default (no ADR needed; all inside ADR-0089):
  - The mirrored test's assertions are a delegate on the case (`CheckMirroredAssertions`), applied to upstream's run
    only, so a Surl defect is decided before the port is judged (decision 5).
  - `-w` time values: the case's `-w`/`--write-out` format is turned into a pattern and only the `%{time_*}` and
    `%{speed_*}` values are replaced; a `-w @file` format is left alone (the case would need to inline it).
  - Stderr: only lines containing one of `AssertedStandardErrorFragments` are compared; none by default.
  - `surl` notes: its own port, any port beside `127.0.0.1`, `[::1]` or `localhost`, and `HH:mm:ss.ffffff` stamps.
  - Request bytes: `CurlPortSurlSide.ClientRequestBytes` is the place BL-579 fills; compared only when both runs
    carry them, with only the `surl` port normalised so far.
  - "Release difference" applies only when the case names a `ReleaseDifference` and the paired build's version line
    does not start `curl 8.21.0 `.
  - ADR-0089 decision 3 removes `CURL_EXTENSIONS`/`SURL_EXTENSIONS`; the runners can only set variables and `surl`
    runs in-process, so the helper is Inconclusive when either is set in the test process rather than editing the
    process environment under parallel tests.
  - The Surl commit comes from `git -C <root> rev-parse HEAD`, or a named "unknown" when git fails.
- Smoke test on this machine (2026-11-04): `curl -s {surl}/hello.txt`, upstream 0E7737…8778 and port b42c319b:
  agreement.
- Coverage: `Surl.Conformance.UnitLibrary` 100% line, 100% branch. `Measure-CodeQuality.ps1` still reports 2 members
  that were already over complexity 10 (`UpstreamCurlBuildFeatures`, from BL-389); filed as BL-599.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. One Phase 7 case now runs through pinned upstream curl and the pinned Curl port, each against its own surl, and ends in an ADR-0089 verdict or finding
