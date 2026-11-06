---
id: BL-591
title: Run the runnable upstream test cases with the pinned Curl port as the client beside upstream curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-394, BL-879, BL-396, BL-397]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/CurlPort/Upstream-Test-Cases.md, Documentation/Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md]
requirement: none
created: 2026-11-04
completed: 2026-11-06
---
# BL-591 — Run the runnable upstream test cases with the pinned Curl port as the client beside upstream curl

## Goal

Every upstream test case at tag `curl-8_21_0` that Surl's runner plays with pinned upstream curl as the
client (BL-392 to BL-397) is also run with the pinned Curl port as the client, against the same server
half. Each case where upstream passes and the port fails the case's verify section is a port finding.
`Documentation/Conformance/CurlPort/Upstream-Test-Cases.md` states the port's pass count beside upstream's,
per protocol family, and lists each finding in BL-576's ADR decision 6 form.

## Context

- Rules: BL-576's ADR, decisions 1 (corpus part b), 5 and 6. ADR-0080 sets how Surl plays the server half
  of upstream test cases, and FR-053 states the upstream pass rate. Upstream curl's result for each case
  stays the oracle. A case that upstream curl does not pass against Surl is not a port finding. It is
  already Surl's own work on its board.
- The runner is BL-392's, extended by BL-394 to BL-397, in `Conformance/Surl.Conformance.UnitLibrary`.
  Today it takes an upstream curl location. Give it a client seam so the same case can run with BL-406's
  port runner, without letting an upstream-curl run accept the port's binary (ADR-0083 decision 2). The
  passing list `Documentation/Conformance/upstream-test-cases-passing.<platform>.txt` (BL-393) is the set of
  cases to run.
- A case's verify section compares the client's protocol bytes and output with what upstream's test expects.
  These were written for upstream curl, so a port mismatch there is a real finding, not a Surl change.
- Windows only until another port pin exists (ADR's decision 8). Every run is `[TestCategory("Integration")]`.

## Acceptance criteria

- [x] Named unit tests, with no process, show that the runner plays a case with the port's runner through
      the new seam, and that the upstream locator still refuses the port's binary.
- [x] An `[TestCategory("Integration")]` test runs every case in `upstream-test-cases-passing.windows.txt`
      (or the file BL-393 names for Windows) with the port. On Windows with the port pinned, it passes in the
      ADR's decision 5 sense.
- [x] `Documentation/Conformance/CurlPort/Upstream-Test-Cases.md` names the port commit, upstream build and
      Surl commit, gives upstream's and the port's pass counts per protocol family, and lists each finding
      with its test case number.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Conformance.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

- **Result (2026-11-06, Windows):** 1,041 cases on `upstream-test-cases-passing.win-x64.txt` (the file
  BL-393 names for Windows). Upstream passed all 1,041; the port passed 1,028; 11 port defects in three
  findings (UTC-1 cookie domain saved host-only: 798, 1136; UTC-2 empty SASL EXTERNAL response sent as an
  empty line instead of `=`: 838, 884, 943; UTC-3 `--libcurl` option order: 1402, 1404, 1405, 1406, 1407,
  1465); 2 not comparable (1451, 1850: the port's build has no `smb`). Two runs, same result; each about
  4 minutes.
- **Seam:** `IUpstreamTestCaseClient` with `UpstreamCurlTestCaseClient` (wraps only `UpstreamCurlRunner`)
  and `CurlPortTestCaseClient` (wraps only `CurlPortClientRunner`). The tests' `CaseRun` and `CaseClient`
  take the seam; `RunBesideAsync` plays each case with upstream, then the port, each with fresh server
  halves and its own folder. Named no-process tests: `CurlPortTestCaseClientTests` (`PlayCase_*`,
  `RequirePinned_PortsBinary_UpstreamLocatorStillRefusesIt`).
- **Decision (ADR-0089 amendment 7):** the port runs each case as runtests would run it - its own
  `--version` gives `%VERSION`/`%VERNUM` (8.22.0), `%CURL`, the `%if` features and the selection
  (`CurlVersionReport`, `UpstreamCurlBuildFeatures.For(CurlVersionReport)`,
  `UpstreamTestCaseSelectionRules.ForClient`). runtests' `subvariables` (servers.pm at the tag) sets
  `VERSION` from the tested curl's own `curl <version>` line; holding the port to `8.21.0` would make every
  `User-Agent: curl/%VERSION` case a false finding. A case runtests would skip for the port is "not
  comparable", a reason the amendment adds. Upstream's runs now also take `%VERSION`/`%VERNUM` from their
  pin's version line (8.21.0 for both Windows pins, so no change); `%VERNUM` was unset before. Upstream's
  own suite (`UpstreamTestCasesAgainstSurlTests`) was re-run after the refactor and passed.
- **Default taken:** `UpstreamCurlBuildFeatures.ReleaseBuildOptions` (probed on the Windows reference pin)
  are taken to hold for the port too, since it is a release build; none of the 11 findings depends on one.
- **touches:** added ADR-0089 for amendment 7; no task in Doing names it.
- `CurlPortClientRunner` now reads stdout and stderr before writing stdin (through
  `UpstreamCurlRunner.WriteStandardInputAsync`), as upstream's runner does, so a case with a large
  `<stdin>` cannot deadlock the port.
- `dotnet format --verify-no-changes` still reports line-ending and import-order findings in files this
  task did not touch (`UpstreamCurlLogsInToSurlOverHttpTests.cs`, `UpstreamCurlFetchesFromSurlOverHttp2Tests.cs`,
  `UpstreamTestCaseServerHalves.cs`, and the existing import order of `UpstreamTestCasesAgainstSurlTests.cs`);
  left as they are.

## Log

- 2026-11-04: Created.
- 2026-11-05: depends-on BL-395 replaced by BL-879, the last task of its split (BL-395 deferred: timed out after 120 min; Stewart asked for the split 2026-11-05).
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. The pinned Curl port plays all 1,041 passing upstream test cases beside upstream curl; 11 port defects listed in Upstream-Test-Cases.md
