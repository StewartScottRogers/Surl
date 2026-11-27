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
completed: 2026-11-26
---
# BL-1807 — List every tool surl-mcp registers in Application/Surl.Mcp/CLAUDE.md

## Goal

`Application/Surl.Mcp/CLAUDE.md` names every tool `surl-mcp` registers.

## Context

Found by BL-1799, which counted 25 names there against the 40 `registry.AddTool` calls in `Application/Surl.Mcp/Program.cs`. The README table written by BL-1799 lists all 40.

## Acceptance criteria

- [x] Every tool registered by a `registry.AddTool` call in `Application/Surl.Mcp/Program.cs` is named in `Application/Surl.Mcp/CLAUDE.md`.

## Notes

- Verified 2026-11-26: each of the 40 `registry.AddTool` calls was resolved to its tool class's `Name` constant, and all 40 names are in `Application/Surl.Mcp/CLAUDE.md` (15 had been missing).
- The `Program` bullet now lists the tools in registration order with a table of the deciding ADRs, and its nearby statements about the root directory, the `serverInfo` version and `instructions` were corrected to match the code.
- Docs only. `dotnet build` clean (0 warnings); fast tests: 30134 passed, 3 skipped, 0 failed.
- Follow-up drift filed: BL-1822, BL-1823, BL-1824, BL-1825.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. Application/Surl.Mcp/CLAUDE.md names all 40 tools surl-mcp registers
