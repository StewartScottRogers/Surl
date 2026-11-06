---
id: BL-746
title: Bring McpStreamSession.CancelRequest to complexity 10 or less
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.McpServing.UnitLibrary, Protocols/Http/Surl.McpServing.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-746 — Bring McpStreamSession.CancelRequest to complexity 10 or less

## Goal

`McpStreamSession.CancelRequest` measures a cyclomatic complexity of at most 10, as the quality gates require.

## Context

`Measure-CodeQuality.ps1 -Library Surl.McpServing.UnitLibrary` (run in BL-465, 2026-11-05) reports
`McpStreamSession.CancelRequest(JsonElement?)` at `Protocols/Http/Surl.McpServing.UnitLibrary/McpStreamSession.cs:212`
with complexity 12 (line and branch coverage 100%). It predates BL-465. Extract a private method (for example the
reading of `params.requestId`) without changing behaviour; the existing `McpStreamSessionTests` must stay green.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.McpServing.UnitLibrary` reports 0 failing members.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-05: Created.
