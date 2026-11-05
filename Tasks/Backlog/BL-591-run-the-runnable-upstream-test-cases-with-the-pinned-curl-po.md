---
id: BL-591
title: Run the runnable upstream test cases with the pinned Curl port as the client beside upstream curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-394, BL-395, BL-396, BL-397]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/CurlPort/Upstream-Test-Cases.md]
requirement: none
created: 2026-11-04
completed:
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

- [ ] Named unit tests, with no process, show that the runner plays a case with the port's runner through
      the new seam, and that the upstream locator still refuses the port's binary.
- [ ] An `[TestCategory("Integration")]` test runs every case in `upstream-test-cases-passing.windows.txt`
      (or the file BL-393 names for Windows) with the port. On Windows with the port pinned, it passes in the
      ADR's decision 5 sense.
- [ ] `Documentation/Conformance/CurlPort/Upstream-Test-Cases.md` names the port commit, upstream build and
      Surl commit, gives upstream's and the port's pass counts per protocol family, and lists each finding
      with its test case number.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Conformance.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
