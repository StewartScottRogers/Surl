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
completed: 2026-11-05
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

- [x] The test class exists with one test per test in `UpstreamCurlTalksToSurlOverRtspTests`, each
      `[TestCategory("Integration")]`, naming the mirrored test in its doc comment, and Inconclusive when
      either pinned binary is absent.
- [x] On Windows with both binaries present, every test passes in the ADR's decision 5 sense.
- [x] `Documentation/Conformance/CurlPort/Rtsp.md` names the port commit and upstream build measured, lists
      each finding or states that there are none, and says where the libcurl rows are measured.
- [x] Any Surl defect found is filed as a task and named in Notes.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Built on the FTP precedent (`CurlPortBesideUpstreamCurlOverFtpTests`, BL-581): 16 cases (15 tests, one
  with two data rows), each `[Description("Mirrors …")]`, the same `-sS -m 20` arguments, `surl` options and
  `clip.bin` as the mirrored test. Measured on Windows 2026-11-05: 13 agreements, 3 port defects, no Surl
  defect, so no Surl task filed.
- Port finding RTSP-1 (recorded in `Documentation/Conformance/CurlPort/Rtsp.md`): the port does not answer
  surl's 401 Digest challenge over RTSP; it sends one `OPTIONS *` and stops (`--digest` and `--anyauth`).
  BL-592's handoff carries it to the port's board.
- Choice: `Options_AnyAuth_Exits0WithDigest`'s asserted stderr line `> Authorization: Digest …` carries a
  per-run cnonce and response that the stderr normaliser does not replace, so it is not compared as
  stderr; the relay compares the same header in the request bytes with both normalised (ADR-0089
  decision 4). Checked against upstream's run in the mirrored assertion still.
- Choice: the upload case checks `up.sdp` is absent through `arrangeAsync`'s disposal, as the FTP class
  checks the `MKD`/`RMD` directory, since each run's `surl` is stopped before the verdict returns.
- Libcurl rows left to BL-597 (ADR-0089 decision 1 (c)); `Rtsp.md` says so.
- `dotnet format --verify-no-changes` on the project reports import-order errors in
  `UpstreamTestCasesAgainstSurlTests.cs` and `UpstreamTestCaseServerHalves.cs`, outside this task's
  `touches`; the new file is clean.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. RTSP Phase 7: 16 cases run through upstream curl and the Curl port; 13 agree, finding RTSP-1 (port ignores Digest challenge) recorded in Rtsp.md
