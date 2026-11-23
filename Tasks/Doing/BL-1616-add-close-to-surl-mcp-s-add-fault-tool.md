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
completed:
---
# BL-1616 — Add close to Surl.Mcp's add_fault tool

## Goal

The add_fault tool of Surl.Mcp takes an optional close argument.

## Context

From BL-1608; decided in ADR-0188 (Documentation/Planning/Decisions). ADR-0188 decision 4. File: Application/Surl.Mcp.UnitLibrary/AddFaultTool.cs.

## Acceptance criteria

- [ ] `close` is an optional string argument, `reset` or `graceful`, with the refusals of ADR-0188 decision 4.
- [ ] Tool schema text names it.
- [ ] Tests for accepted and refused values.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Backlog. Needs SurlFault.Close from BL-1615 (Surl.Api, in Doing); added to depends-on
- 2026-11-23: Backlog -> Doing.
