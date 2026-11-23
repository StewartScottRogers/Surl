---
id: BL-1615
title: Add Close to Surl.Api's SurlFault
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1608, BL-1611]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: FR-062
created: 2026-11-22
completed:
---
# BL-1615 — Add Close to Surl.Api's SurlFault

## Goal

Surl.Api SurlFault has a Close property.

## Context

From BL-1608; decided in ADR-0188 (Documentation/Planning/Decisions). ADR-0188 decision 4. Files: Application/Surl.Api/SurlFault.cs, Application/Surl.Api/README.md.

## Acceptance criteria

- [ ] `SurlFault.Close` (`FaultClose`, default `Reset`) init property, read from a rule and written by ToString after `after-bytes`.
- [ ] README states it.
- [ ] Tests for the property, the item text and a rule round trip.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
