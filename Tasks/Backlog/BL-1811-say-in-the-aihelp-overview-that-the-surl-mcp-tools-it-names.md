---
id: BL-1811
title: Say in the --aihelp overview that the surl-mcp tools it names are only some of them
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-26
completed:
---
# BL-1811 — Say in the --aihelp overview that the surl-mcp tools it names are only some of them

## Goal

The `surl --aihelp` overview presents `start_server`, `list_servers`, `stop_server` and `get_exchanges` as
some of `surl-mcp`'s tools, not as all of them.

## Context

Found while running BL-1807 (checked on 2026-11-26). `AiHelpProse.OverviewHosts`
(`Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`) says "its tools `start_server`, `list_servers`,
`stop_server` and `get_exchanges` describe themselves", while `Application/Surl.Mcp/Program.cs` registers 40.
The overview does not list them all: `tools/list` does, each with its own description (ADR-0103 decision 6,
ADR-0099 decision 9, one sentence on `Surl.Mcp`).

The sentence becomes: "An MCP client starts them through `surl-mcp`, the `Surl.Mcp` executable, on standard
input and output: its tools, `start_server`, `list_servers`, `stop_server` and `get_exchanges` among them,
describe themselves, and it serves these pages as the `surl://aihelp` resources."

The paragraph is pinned by `OverviewHostsParagraph` in `Application/Surl.Cli.UnitTests/AiHelpTextTests.cs`,
used by `Answer_NoTopic_PointsAtSurlApisStartingTypeAndSurlMcpsExecutableAndTools` and the whole-overview test.
No store, event or fault applies: this task changes help text only.

## Acceptance criteria

- [ ] `AiHelpProse.OverviewHosts` holds the sentence quoted in Context.
- [ ] `OverviewHostsParagraph` in `AiHelpTextTests` pins the same text, and the tests of `Surl.Cli.UnitTests`
      pass.
- [ ] `dotnet build Application/Surl.Cli.UnitTests -warnaserror` is clean.

## Notes

## Log

- 2026-11-26: Created.
