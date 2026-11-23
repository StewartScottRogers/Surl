---
id: BL-1614
title: Carry a fault's close through Surl.Composition's plan and item text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1608, BL-1611]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-062
created: 2026-11-22
completed:
---
# BL-1614 — Carry a fault's close through Surl.Composition's plan and item text

## Goal

Surl.Composition builds and writes back a fault rule with its close.

## Context

From BL-1608; decided in ADR-0188 (Documentation/Planning/Decisions). ADR-0188 decision 4. Files: Application/Surl.Composition.UnitLibrary/FaultPlanComposition.cs and FaultItemText.cs (the item text the control endpoint /v1/faults carries).

## Acceptance criteria

- [ ] A configured `close=graceful` reaches `FaultRule.Close`.
- [ ] `FaultItemText` writes `close=graceful` back and omits the key for `Reset`.
- [ ] Tests for both directions; gates hold.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
