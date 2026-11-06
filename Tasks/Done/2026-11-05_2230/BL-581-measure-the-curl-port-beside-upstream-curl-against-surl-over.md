---
id: BL-581
title: Measure the Curl port beside upstream curl against surl over FTP and FTPS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-579]
touches: [Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverFtpTests.cs, Conformance/Surl.Conformance.UnitTests/PinnedCurlPortBesideUpstreamCurl.cs, Documentation/Conformance/CurlPort/Ftp.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-581 — Measure the Curl port beside upstream curl against surl over FTP and FTPS

## Goal

`Integration` tests run every FTP and FTPS conversation that pinned upstream curl already runs against a
live `surl`, through both pinned upstream curl and the pinned Curl port, using BL-578's helper.
`Documentation/Conformance/CurlPort/Ftp.md` lists every port finding in BL-576's ADR decision 6 form.

## Context

- Rules: BL-576's ADR (corpus, pairing, what is compared, verdicts, finding form). ADR-0003 decision 6
  applies: upstream curl is right, a disagreement is the port's defect, and **Surl is never changed to make
  the port pass**. If upstream's own run fails, that is a Surl defect. File it on Surl's board as a new task
  and do not record it as a port finding.
- The cases mirror these existing classes in `Conformance/Surl.Conformance.UnitTests`, one Phase 7 test per
  upstream test, each naming the test it mirrors: `UpstreamCurlFetchesFromSurlOverFtpTests`,
  `UpstreamCurlLogsInToSurlOverFtpTests` and `UpstreamCurlUploadsToSurlOverFtpTests`. Do not edit them.
- Data connections: the ADR's decision 4 says whether `PASV`, `EPSV` and active-mode data connections go
  through BL-579's relay or are compared on the control connection only. Follow it.
- Helper: BL-578's `PinnedCurlPortBesideUpstreamCurl` (or the name the ADR chose). The port binary:
  `CurlPortBuilds.json` (BL-577). If the ADR named the test class or the findings file differently, use its
  names and correct this task's `touches`.

## Acceptance criteria

- [x] The test class exists with one test per test in the three classes above, each
      `[TestCategory("Integration")]`, naming the mirrored test in its doc comment, and Inconclusive when
      either pinned binary is absent.
- [x] On Windows with both binaries present, every test passes in the ADR's decision 5 sense.
- [x] `Documentation/Conformance/CurlPort/Ftp.md` names the port commit and upstream build measured, and
      lists each finding, or states that there are none.
- [x] Any Surl defect found is filed as a task and named in Notes.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Result on Windows, 2026-11-05: 43 cases, 43 agreements (two full runs). No port defect, no Surl
  defect, so no task filed. `Ftp.md`'s findings table is empty and says so. The three off-Windows
  listing twins are mirrored too, with their `[OSCondition]`; they are Inconclusive off Windows until a
  port pin exists there (ADR-0089 decision 8).
- `touches` widened to `PinnedCurlPortBesideUpstreamCurl.cs` (no task in Doing names it; BL-580 names
  only its own HTTP test class and `Http.md`). Two additions, both backwards compatible:
  - files below the top folder (`dir/b.txt`, `d1/d2/a.txt`) get their parent folders created
    (`ParentDirectoriesOf`); before this the helper passed no subdirectories, so a nested file failed to write;
  - an optional `arrangeAsync` per run, after `surl` starts and before the client, disposed before `surl`
    stops. The `--max-connections 1` case holds a connection with it; the `MKD`/`RMD` case checks with it
    that folder `x` is gone.
- Decision (sensible default, no ADR needed - it is measurement setup, not behaviour): every served file
  and folder gets one fixed modification time (2026-01-02T03:04:05Z) before each client runs. Without it,
  `MLSD`'s `modify=` fact differed between the two runs, whose `surl`s wrote their content seconds apart,
  and showed up as a false port defect; `-I`'s `Last-Modified` and the long listing's date carry the same
  risk at a second or minute boundary.
- Case names: the findings table's first column is the method name, with `[data row]` appended for a
  data-driven test (`Download_EachDataConnectionMode_WritesTheFile[-P -]`), so each row can be listed alone.
- The findings reader lives in this test class for now. BL-580 (HTTP) is building its own beside it; once
  several families exist, a shared reader is a small cleanup.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The Curl port runs all 43 FTP and FTPS conversations beside pinned upstream curl on Windows and agrees on every one; Ftp.md records no findings
