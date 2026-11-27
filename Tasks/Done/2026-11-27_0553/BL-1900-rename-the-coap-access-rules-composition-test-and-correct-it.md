---
id: BL-1900
title: Rename the CoAP access rules composition test and correct its conformance row
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1864]
touches: [Application/Surl.Composition.UnitTests, Documentation/Conformance/Extended/Coap.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1900 — Rename the CoAP access rules composition test and correct its conformance row

## Goal

The composition test of the `coap` access rules and its conformance row name the question the rules ask: whether a
login is required to read.

## Context

- ADR-0132 amendment 5, decision 9.
- `CoapsCompositionTests.CoapAccessRulesFor_Policy_IsAnonymousSwitchAndWhetherAnAccountExists` and row
  `Coap:ADR0096-6-1` of `Documentation/Conformance/Extended/Coap.md` say "whether an account exists". BL-1864 renames
  the property but names neither. Found by BL-1862.

## Acceptance criteria

- [x] The test's name and body say it checks the anonymous switch and whether a login is required to read, and it
      passes.
- [x] Row `Coap:ADR0096-6-1` cites the test by its new name and says the same, not "whether an account exists".

## Notes

- BL-1864 (commit `4b5deb1ec`) had already renamed the test to
  `CoapAccessRulesFor_Policy_IsAnonymousSwitchAndWhetherAReadNeedsALogin` and corrected row `Coap:ADR0096-6-1`; the
  Context above describes the state before it. All three tests the row cites exist.
- Left to do here: the body compared `IsLoginRequiredToRead()` with the input `hasAccount`. It now compares it with
  its own data column, `expectedLoginRequiredToRead`, so the body names the expected answer. Same values per row.
- Verify: build clean (0 warnings), `dotnet format --verify-no-changes` clean, fast tests 0 failed
  (`Surl.Composition.UnitTests` 937 passed). No Integration run: a test-only change.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The coap access rules composition test and row Coap:ADR0096-6-1 name whether a login is required to read
