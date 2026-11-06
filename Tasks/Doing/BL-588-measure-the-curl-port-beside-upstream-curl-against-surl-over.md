---
id: BL-588
title: Measure the Curl port beside upstream curl against surl over RTSP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-579]
touches: [Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverRtspTests.cs, Documentation/Conformance/CurlPort/Rtsp.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-588 — Measure the Curl port beside upstream curl against surl over RTSP

## Goal

`Integration` tests run every `rtsp` conversation that the pinned upstream curl tool already runs against a
live `surl`, through both pinned upstream curl and the pinned Curl port, using BL-578's helper.
`Documentation/Conformance/CurlPort/Rtsp.md` lists every port finding in BL-576's ADR decision 6 form.

## Context

- Rules: BL-576's ADR (corpus, pairing, what is compared, verdicts, finding form). ADR-0003 decision 6
  applies: upstream curl is right, a disagreement is the port's defect, and **Surl is never changed to make
  the port pass**. If upstream's own run fails, that is a Surl defect. File it on Surl's board as a new task
  and do not record it as a port finding.
- The cases mirror `UpstreamCurlTalksToSurlOverRtspTests` in `Conformance/Surl.Conformance.UnitTests` (the
  tool rows of ADR-0074 decision 11), one Phase 7 test per upstream test, each naming the test it mirrors.
  Do not edit it.
- The libcurl rows (`PinnedLibcurlTalksToSurlOverRtspTests`, ADR-0074 Amendment 1) are measured here only if
  BL-576's ADR decision 1 says how. The port's `Libcurl` is a managed API, not a native `libcurl-4.dll`. If
  the ADR filed a separate task for a libcurl driver, leave those rows to it.
- Helper: BL-578's `PinnedCurlPortBesideUpstreamCurl` (or the name the ADR chose). The port binary:
  `CurlPortBuilds.json` (BL-577). If the ADR named the test class or the findings file differently, use its
  names and correct this task's `touches`.

## Acceptance criteria

- [ ] The test class exists with one test per test in `UpstreamCurlTalksToSurlOverRtspTests`, each
      `[TestCategory("Integration")]`, naming the mirrored test in its doc comment, and Inconclusive when
      either pinned binary is absent.
- [ ] On Windows with both binaries present, every test passes in the ADR's decision 5 sense.
- [ ] `Documentation/Conformance/CurlPort/Rtsp.md` names the port commit and upstream build measured, lists
      each finding or states that there are none, and says where the libcurl rows are measured.
- [ ] Any Surl defect found is filed as a task and named in Notes.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
