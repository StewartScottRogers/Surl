---
id: BL-587
title: Measure the Curl port beside upstream curl against surl over SMB and SMBS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-579]
touches: [Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverSmbTests.cs, Documentation/Conformance/CurlPort/Smb.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-587 — Measure the Curl port beside upstream curl against surl over SMB and SMBS

## Goal

`Integration` tests run every `smb` and `smbs` conversation that pinned upstream curl already runs against a
live `surl`, through both pinned upstream curl and the pinned Curl port, using BL-578's helper.
`Documentation/Conformance/CurlPort/Smb.md` lists every port finding in BL-576's ADR decision 6 form.

## Context

- Rules: BL-576's ADR (corpus, pairing, what is compared, verdicts, finding form). ADR-0003 decision 6
  applies: upstream curl is right, a disagreement is the port's defect, and **Surl is never changed to make
  the port pass**. If upstream's own run fails, that is a Surl defect. File it on Surl's board as a new task
  and do not record it as a port finding.
- The cases mirror `UpstreamCurlTransfersFilesWithSurlOverSmbTests` in `Conformance/Surl.Conformance.UnitTests`,
  one Phase 7 test per upstream test, each naming the test it mirrors. Do not edit it.
- The reference build has no `smb`. The upstream side is ADR-0030's static-curl 8.21.0 Windows build, which
  `PinnedUpstreamCurl.RunForProtocolAsync` already picks. The pairing follows the ADR's decision 2.
- The NTLMv1 session setup carries a client challenge and a timestamp. Compare them as the ADR's decision 4
  normalises them.
- Helper: BL-578's `PinnedCurlPortBesideUpstreamCurl` (or the name the ADR chose). The port binary:
  `CurlPortBuilds.json` (BL-577). If the ADR named the test class or the findings file differently, use its
  names and correct this task's `touches`.

## Acceptance criteria

- [ ] The test class exists with one test per test in `UpstreamCurlTransfersFilesWithSurlOverSmbTests`, each
      `[TestCategory("Integration")]`, naming the mirrored test in its doc comment, and Inconclusive when
      either pinned binary is absent.
- [ ] On Windows with both binaries present, every test passes in the ADR's decision 5 sense.
- [ ] `Documentation/Conformance/CurlPort/Smb.md` names the port commit and upstream build measured, and
      lists each finding, or states that there are none.
- [ ] Any Surl defect found is filed as a task and named in Notes.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
