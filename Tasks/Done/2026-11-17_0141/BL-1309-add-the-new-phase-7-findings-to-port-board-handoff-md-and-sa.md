---
id: BL-1309
title: Add the new Phase 7 findings to Port-Board-Handoff.md and say which the new commit fixed
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1306, BL-1307, BL-1308]
touches: [Documentation/Conformance/CurlPort/Port-Board-Handoff.md, Documentation/Conformance/CurlPort/README.md]
requirement: FR-057
created: 2026-11-16
completed: 2026-11-17
---
# BL-1309 — Add the new Phase 7 findings to Port-Board-Handoff.md and say which the new commit fixed

## Goal

`Port-Board-Handoff.md` lists each new finding from the re-measure on every platform and says which earlier findings the new commit fixed.

## Context

ADR-0089 decision 7 (handoff form) and Amendment 8. Source: the findings sections written by BL-1306, BL-1307 and BL-1308.

## Acceptance criteria

- [x] Every finding in the platform sections of the family files appears in the handoff in decision 7's form.
- [x] The handoff names the new commit and lists the findings it fixed.
- [x] Stewart is told the handoff is ready and how many findings it holds (in the task's Notes).

## Notes

- Port commit 74f3f45 fixed none of the 23 earlier findings and added one (UTC-4, test251), now handoff entry 24. linux-x64 and osx-arm64 runs record no new finding (linux failures await triage BL-1343, which adds its own entries). Added README.md to touches to bring its counts (24 findings, UTC row 1,185 cases) in line.
- Stewart: the handoff is ready and holds 24 findings.

## Log

- 2026-11-16: Created.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. Handoff has entry 24 (UTC-4, new at 74f3f45) and says the new commit fixed none of the 23 earlier findings
