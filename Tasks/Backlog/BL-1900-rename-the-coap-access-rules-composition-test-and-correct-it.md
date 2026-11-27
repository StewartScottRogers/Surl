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
completed:
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

- [ ] The test's name and body say it checks the anonymous switch and whether a login is required to read, and it
      passes.
- [ ] Row `Coap:ADR0096-6-1` cites the test by its new name and says the same, not "whether an account exists".

## Notes

## Log

- 2026-11-27: Created.
