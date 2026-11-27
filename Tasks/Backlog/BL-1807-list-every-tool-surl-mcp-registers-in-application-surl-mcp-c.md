---
id: BL-1807
title: List every tool surl-mcp registers in Application/Surl.Mcp/CLAUDE.md
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Mcp/CLAUDE.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-1807 — List every tool surl-mcp registers in Application/Surl.Mcp/CLAUDE.md

## Goal

`Application/Surl.Mcp/CLAUDE.md` names every tool `surl-mcp` registers.

## Context

Found by BL-1799, which counted 25 names there against the 40 `registry.AddTool` calls in `Application/Surl.Mcp/Program.cs`. The README table written by BL-1799 lists all 40.

## Acceptance criteria

- [ ] Every tool registered by a `registry.AddTool` call in `Application/Surl.Mcp/Program.cs` is named in `Application/Surl.Mcp/CLAUDE.md`.

## Notes

## Log

- 2026-11-26: Created.
