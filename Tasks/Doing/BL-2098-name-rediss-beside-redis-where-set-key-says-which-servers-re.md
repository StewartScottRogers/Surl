---
id: BL-2098
title: Name rediss beside redis where set_key says which servers refuse it
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2098 — Name rediss beside redis where set_key says which servers refuse it

## Goal

An agent reading `set_key`'s description learns that a server with neither a `redis` nor a `rediss` listen URL
refuses the seed.

## Context

Noticed while working BL-2074 (2026-11-28) and read from source the same day.

- `Application/Surl.Mcp.UnitLibrary/SetKeyTool.cs`, `SetKeyTool.Description`, ends "A server with no redis listen
  URL refuses it." The keyspace is held when a `redis` or `rediss` listen URL is served (`ComposeStores` in
  `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs`; ADR-0158 Amendment 1), so a server
  with only a `rediss` URL accepts the seed.
- `Application/Surl.Mcp.UnitLibrary/CLAUDE.md`, the `SetKeyTool` entry, says "On a server with no `redis` listen
  URL it ... is `isError`" with the same omission.
- `Application/Surl.Mcp/README.md` already names `redis` or `rediss` and does not change.
- No test pins the sentence: `ProgramTests` compares `tools/list` with the constant, and
  `ToolDescriptionByteRuleTests` asserts only the 16 MiB and byte wording.
- The description is output `surl-mcp` sends to agents. The tool's behaviour, the refusal text, the stores and the
  events do not change, so there is nothing new for a test to seed, reset, observe or inject.

## Acceptance criteria

- [ ] `SetKeyTool.Description` says that a server with no `redis` or `rediss` listen URL refuses the call.
- [ ] A test in `Application/Surl.Mcp.UnitTests` asserts that sentence of `SetKeyTool.Description`.
- [ ] The `SetKeyTool` entry in `Application/Surl.Mcp.UnitLibrary/CLAUDE.md` names both schemes.
- [ ] The tests of `Application/Surl.Mcp.UnitTests` pass with `--filter "TestCategory!=Integration"`, and
      `dotnet build Application/Surl.Mcp.UnitTests -warnaserror` is clean.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
