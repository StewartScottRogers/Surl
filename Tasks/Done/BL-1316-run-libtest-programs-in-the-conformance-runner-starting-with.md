---
id: BL-1316
title: Run libtest programs in the conformance runner, starting with 2402, 2404, 2407 and 2502 (ADR-0174)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1311, BL-1315]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1316 — Run libtest programs in the conformance runner, starting with 2402, 2404, 2407 and 2502 (ADR-0174)

## Goal

The conformance runner runs a case's libtest program from the pinned libtest entry, so 2402, 2404, 2407 and 2502 get verdicts on Linux.

## Context

ADR-0174 decisions 3, 5 and 6; ADR-0080 skip reasons; needs the Debug build runner path of BL-1311 and the pins of BL-1315.

## Acceptance criteria

- [x] (runner path only; the Linux verdicts moved to BL-1318) 2402, 2404, 2407 (lib2405) and 2502 run on Linux and their verdicts are in Upstream-Test-Case-Pass-Rate.md.
- [x] Windows and macOS keep skip reason libtest; a missing program gives libtest-not-built:<name>; a unit test pins each.

## Notes
Docs only; ADR-0074 gained "Amendment - Choices made while building" (items 1 to 8), unnumbered so it cannot collide with BL-1294's amendment. Tasks read (15 reference the library; BL-286 is the ADR itself):
- BL-286: no choice (it wrote the ADR). BL-219, BL-319, BL-396: no choice (reference lists, docs, RTSP libtests skipped).
- BL-313: choice recorded as items 1 to 4 (its interim 501s for later methods are obsolete and left out).
- BL-314: choice recorded as items 5 to 7.
- BL-315: already in ADR-0074 (decision 5, folded by BL-338); its interim 501 for mode=record is obsolete.
- BL-316: already in ADR-0074 (decision 6, folded by BL-339); its HttpMessage `PeekByteAsync` is not an RTSP answer.
- BL-337: already in ADR-0074 (Amendment 1, decision 5 amended). BL-338, BL-339: no choice (the folding tasks).
- BL-661: choice recorded as item 8.
- BL-793: already in an ADR (ADR-0134). BL-902: already in an ADR (ADR-0141 amendment 1).
- BL-1256: no choice (no wire change).
All 12 tests named were found by grep in Protocols/Http/Surl.Protocol.Rtsp.UnitTests. No choice looked wrong against RFC 2326 or lib/rtsp.c, so no task filed.
- Runner path done: `UpstreamTestCaseLibtests` (admitted cases 2402, 2404, 2407, 2502; program name from `<tool>`), `UpstreamCurlLocator.LocateLibtest` (verifies the pinned `libtests` hash), `UpstreamLibtestTestCaseClient` (runs `libtests <program> <args>`; curl 8.21.0 links every libtest into that one binary), `UpstreamLibtestAvailability` on the selection rules, and the new skip reason `libtest-not-built:<program>`. The Integration runner routes an admitted case to it where a Debug build is pinned and the libtests file is present.
- Windows and macOS pin no libtest entry, so every libtest case keeps `libtest`; a pinned-but-missing binary gives `libtest-not-built:<name>`; tests in UpstreamTestCaseSelectorTests and UpstreamCurlLocatorTests pin each.
- Not measured: this lane runs on Windows and the Linux Debug build is not here, so 2402, 2404, 2407 and 2502 have no verdict yet and the `libtests <program>` calling convention is taken from curl's tests/libtest/first.c, unmeasured. Filed BL-1318 to run on Linux and record the verdicts.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Runner path built; Linux verdicts are BL-1318
