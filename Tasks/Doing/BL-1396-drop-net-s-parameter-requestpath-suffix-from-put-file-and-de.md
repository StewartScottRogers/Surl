---
id: BL-1396
title: Drop .NET's (Parameter 'requestPath') suffix from put_file and delete_file path refusals
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-19
completed:
---
# BL-1396 — Drop .NET's (Parameter 'requestPath') suffix from put_file and delete_file path refusals

## Goal

`surl-mcp`'s `put_file` and `delete_file`, given a path a request may not name, answer a tool error whose text an
agent can act on, without .NET's ` (Parameter 'requestPath')` suffix.

## Context

- Surly cookbook bug report 2026-11-19 06:42, status of the 17:34 report's finding 5: BL-1382 made the bad path a
  tool error (`NotRooted`, `DotSegment`), but the message still ends in ` (Parameter 'requestPath')`, the
  `ArgumentException.Message` format.
- The exception comes from the content store's path mapping (`Foundation/Surl.Content.UnitLibrary/ContentStore.cs`,
  `ContentStore.Control.cs`, `RequestPathSegments.cs`); BL-1382's catch is in `Surl.Mcp.UnitLibrary`
  (`RunningServerTool` and its callers).

## Acceptance criteria

- [ ] The tool error text for `"reports/b.txt"` and for `"/../x.txt"` names the problem and the path the agent
      gave (e.g. `path reports/b.txt must start with /`), with no `(Parameter ` text; built from the refusal kind,
      not by trimming the exception string.
- [ ] Named tests in `Surl.Mcp.UnitTests` pin both texts for `put_file` and `delete_file`.
- [ ] `dotnet build` clean, fast tests green, coverage gates held.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
