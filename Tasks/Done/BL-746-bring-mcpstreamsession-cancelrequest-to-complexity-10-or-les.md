---
id: BL-746
title: Bring McpStreamSession.CancelRequest to complexity 10 or less
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1035]
touches: [Protocols/Http/Surl.McpServing.UnitLibrary, Protocols/Http/Surl.McpServing.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-11
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

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.McpServing.UnitLibrary` reports 0 failing members.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

Extracted private static TryReadRequestId from CancelRequest; behaviour unchanged. SDK 10.0.404 builds fine.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. CancelRequest complexity under 10 via TryReadRequestId; 0 failing members, 244 tests green
