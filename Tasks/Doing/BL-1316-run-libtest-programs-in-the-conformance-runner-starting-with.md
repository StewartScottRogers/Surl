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
completed:
---
# BL-1316 — Run libtest programs in the conformance runner, starting with 2402, 2404, 2407 and 2502 (ADR-0174)

## Goal

The conformance runner runs a case's libtest program from the pinned libtest entry, so 2402, 2404, 2407 and 2502 get verdicts on Linux.

## Context

ADR-0174 decisions 3, 5 and 6; ADR-0080 skip reasons; needs the Debug build runner path of BL-1311 and the pins of BL-1315.

## Acceptance criteria

- [ ] 2402, 2404, 2407 (lib2405) and 2502 run on Linux and their verdicts are in Upstream-Test-Case-Pass-Rate.md.
- [ ] Windows and macOS keep skip reason libtest; a missing program gives libtest-not-built:<name>; a unit test pins each.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
