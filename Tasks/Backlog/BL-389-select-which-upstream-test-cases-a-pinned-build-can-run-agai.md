---
id: BL-389
title: Select which upstream test cases a pinned build can run against surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-388]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-389 — Select which upstream test cases a pinned build can run against surl

## Goal

Given a parsed upstream test case and a pinned build's `UpstreamCurlBuildPins` entry,
`Surl.Conformance.UnitLibrary` decides whether the case can run against surl. If it cannot, it gives
one named skip reason from BL-386's ADR decision 3.

## Context

- Decision: BL-386's ADR, decision 3: the classes out of scope and their reasons.
- Inputs: the case's `<server>` list, `<features>` (including negated `!feature` entries, as
  FILEFORMAT.md describes), `<tool>`, `<command type=...>`, `<precheck>`, `<postcheck>` and
  `<keywords>`, plus upstream's `tests/data/DISABLED` list. The other input is the build's `protocols`
  and `features` lines from `UpstreamCurlBuilds.json`, read by `UpstreamCurlBuildPins`.
- Which servers surl can be today follows from the registered protocol servers and the ADR's
  decision 2. Proxy and SOCKS servers are not built yet.
- FILEFORMAT.md's `<features>` list (tag `curl-8_21_0`, lines 473 to 560) names features that a curl
  binary's `--version` does not show, such as `debug`, `TrackMemory` and `unittest`. Each of them is
  either a skip reason or derived from the pin. Choose per the ADR.

## Acceptance criteria

- [ ] Named tests cover each skip reason with an inline case, and a runnable case for each pinned
      build's platform.
- [ ] A test over every imported case and each of the three reference pins writes the count per skip
      reason and the runnable count to the test log. Notes record the counts.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
