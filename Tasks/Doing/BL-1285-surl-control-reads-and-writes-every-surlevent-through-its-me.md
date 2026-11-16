---
id: BL-1285
title: Surl.Control reads and writes every SurlEvent through its members, not a switch (ADR-0171)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1245, BL-1283]
touches: [Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests]
requirement: FR-060
created: 2026-11-16
completed:
---
# BL-1285 — Surl.Control reads and writes every SurlEvent through its members, not a switch (ADR-0171)

## Goal

The control endpoint queries, filters and writes every SurlEvent in full through the base members and WriteMembers.

## Context

ADR-0171 (BL-1245) decides how a SurlEvent kind reaches the three hosts: the event says what hosts read; hosts keep no per-type switch.

## Acceptance criteria

- [ ] ControlEventKinds.Known is derived from the exported SurlEvent subclasses; ControlEventWriter writes members through an ISurlEventMemberWriter that builds the JSON.
- [ ] A test enumerating the exported subclasses fails when one is not in Known, not filterable by kind, or any of its public properties is missing from its JSON.
- [ ] Build clean, fast tests green, 100% line and branch coverage on the touched libraries.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
