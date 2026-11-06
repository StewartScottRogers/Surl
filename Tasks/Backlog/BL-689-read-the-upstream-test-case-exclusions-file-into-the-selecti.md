---
id: BL-689
title: Read the upstream test-case exclusions file into the selection rules
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-393]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-689 — Read the upstream test-case exclusions file into the selection rules

## Goal

`UpstreamTestCaseSelectionRules.ForBuild` reads `Documentation/Conformance/Upstream-Test-Case-Exclusions.md`
into `TestServerChoices`, so a listed case skips `test-server-choice`.

## Context

- ADR-0080 decision 5. BL-393 created the exclusions file with an empty table (columns: Case,
  Byte verified, Upstream's choice, Surl's answer, Why ...). `TestServerChoices` is empty today.
- A case number in a passing list may be removed only with an exclusions row (decision 5);
  a fast test can check that no case is both excluded and listed as passing.

## Acceptance criteria

- [ ] A parser reads the table's `Case` column (`test<N>` or `<N>`) into a set, with unit tests for a row, an empty table and a malformed row.
- [ ] The suite's run passes that set as `TestServerChoices`, and the pass-rate file's test-server-choice count is the set's size.
- [ ] A fast test fails when a case is both in the exclusions file and in a passing list.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green; the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
