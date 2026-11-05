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
completed:
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

- [ ] Named unit tests cover the comparison with no process and no network. There is one test per verdict
      the ADR names, and one per normalisation rule in its decision 4. Each normalisation test shows that two
      results differing only in that field agree, and that a difference elsewhere is still a finding.
- [ ] A unit test shows that a finding carries every field the ADR's decision 6 lists: upstream build
      SHA-256, port commit and SHA-256, Surl commit, command line and both results.
- [ ] One `[TestCategory("Integration")]` smoke test in `Conformance/Surl.Conformance.UnitTests` measures
      `curl http://127.0.0.1:<port>/hello.txt` against `surl http://`. With both pinned binaries present on
      Windows it reaches a verdict, and without them it is Inconclusive.
- [ ] `Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md` describes the Phase 7 types and the port's role.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Conformance.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
