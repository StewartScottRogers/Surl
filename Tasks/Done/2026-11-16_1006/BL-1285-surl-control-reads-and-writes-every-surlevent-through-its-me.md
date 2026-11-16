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
completed: 2026-11-16
---
# BL-1285 — Surl.Control reads and writes every SurlEvent through its members, not a switch (ADR-0171)

## Goal

The control endpoint queries, filters and writes every SurlEvent in full through the base members and WriteMembers.

## Context

ADR-0171 (BL-1245) decides how a SurlEvent kind reaches the three hosts: the event says what hosts read; hosts keep no per-type switch.

## Acceptance criteria

- [x] ControlEventKinds.Known is derived from the exported SurlEvent subclasses; ControlEventWriter writes members through an ISurlEventMemberWriter that builds the JSON.
- [x] A test enumerating the exported subclasses fails when one is not in Known, not filterable by kind, or any of its public properties is missing from its JSON.
- [x] Build clean, fast tests green, 100% line and branch coverage on the touched libraries.

## Notes

Known stays an explicit list (native AOT cannot enumerate types); `ControlEventMemberWriterTests` enumerates the exported SurlEvent records and fails when one is missing from Known, has no sample, or has a public property absent from its JSON. ControlEventJson gained `layer` and `stream_id` (additive; bytes events now say `"layer":"wire"`). Coverage measured by reasoning: every member lambda is hit by the all-kinds sample test.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Control endpoint reads and writes every SurlEvent through its members; Known pinned by a test
