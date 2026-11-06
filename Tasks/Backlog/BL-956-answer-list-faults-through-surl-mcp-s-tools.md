---
id: BL-956
title: Answer list_faults through Surl.Mcp's tools
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-955]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-956 — Answer list_faults through Surl.Mcp's tools

## Goal

An agent driving `surl-mcp` can call `list_faults` with a `server_id` and see each fault rule of that server as
`{"faults": [{"fault_id", "item", "remaining"}]}`, in plan order.

## Context

- ADR-0124 decision 8's table row for `list_faults`; the other nine store and fault tools landed in BL-812
  (`Application/Surl.Mcp.UnitLibrary/CLAUDE.md`). Build it like them, with `RunningServerTool.Create`, and
  register it in `Application/Surl.Mcp/Program.cs` between `add_fault` and `remove_fault`, the ADR's order.
- Reads BL-955's `SurlServerFaults` listing. `remaining` is `null` for a rule without `times`.
- Mention `list_faults` in `Program.Instructions`, `SurlMcpHelpText.Help` and both projects' `CLAUDE.md`.

## Acceptance criteria

- [ ] Named tests: `list_faults` after a start `--fault` and an `add_fault` lists both with `f1`, `f2`, their items
      and `remaining`; `remaining` drops after a fake request applies a `times` rule; after `remove_fault` the rule
      is gone; an unknown server ID is `isError` with `stop_server`'s text.
- [ ] `ProgramTests`' `tools/list` order test lists `list_faults` between `add_fault` and `remove_fault`, with a
      JSON object input schema and a description.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Mcp.UnitLibrary` keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
