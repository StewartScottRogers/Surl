---
id: BL-1616
title: Add close to Surl.Mcp's add_fault tool
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1608, BL-1611, BL-1615]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: FR-062
created: 2026-11-22
completed: 2026-11-23
---
# BL-1616 — Add close to Surl.Mcp's add_fault tool

## Goal

The add_fault tool of Surl.Mcp takes an optional close argument.

## Context

From BL-1608; decided in ADR-0188 (Documentation/Planning/Decisions). ADR-0188 decision 4. File: Application/Surl.Mcp.UnitLibrary/AddFaultTool.cs.

## Acceptance criteria

- [x] `close` is an optional string argument, `reset` or `graceful`, with the refusals of ADR-0188 decision 4.
- [x] Tool schema text names it.
- [x] Tests for accepted and refused values.

## Notes

- `close` is checked in the tool (any case; non-string refused) with surl's own text, then appended as `;close=<word>` so the reader gives the other refusals (needs after-bytes, wrong kind). Checking first stops a value with `;` smuggling keys.
- `SurlFault.Close` is carried by `ToSurlFault`. The plan itself only keeps it once BL-1614 (Surl.Composition) lands, so tests here do not assert on list_faults showing close=graceful; add that check after BL-1614.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Backlog. Needs SurlFault.Close from BL-1615 (Surl.Api, in Doing); added to depends-on
- 2026-11-23: Backlog -> Doing.
- 2026-11-23: Doing -> Done. add_fault takes optional close (reset|graceful) with ADR-0188 refusals; tests pass
