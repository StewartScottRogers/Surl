---
id: BL-627
title: Serve the surl --aihelp topics as MCP resources from Surl.Mcp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-623, BL-626]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-627 — Serve the surl --aihelp topics as MCP resources from Surl.Mcp

## Goal

An agent connected to `Surl.Mcp` can list and read the same Markdown `surl --aihelp` prints - the overview, each
topic, and `all` - as MCP resources, so it learns the options, schemes, exit codes and security rules the
`start_server` tool takes without running `surl`. A completeness test keeps the resources in step with the topics.

## Context

- Decision: BL-612's ADR, decision 6 (the resource URIs, names and MIME type; planning `surl://aihelp/<topic>`,
  `text/markdown`).
- Source of the text: `Application/Surl.Cli.UnitLibrary`'s `AiHelpText.Answer(string? topic)` (`null` for the
  overview) and `AiHelpTopics.All` (ADR-0046). `Surl.Mcp.UnitLibrary` references `Surl.Cli` for them; nothing is
  copied or re-worded, so the resources cannot drift from `surl --aihelp`.
- Resource providers: `Surl.McpServing` (BL-623), registered by `Surl.Mcp` with one line.
- The port does the same with its `--ai-help` (`AiHelpMcpResourceProvider`, its ADR-0589); copyable, not evidence.

## Acceptance criteria

- [ ] Named tests show: `resources/list` from `RunAsync` lists the overview, one resource per `AiHelpTopics.All`
      entry and `all`; reading each returns exactly the text `AiHelpText.Answer` gives for it; an unknown topic URI
      gets `-32002`.
- [ ] A named completeness test fails if a topic is added to `AiHelpTopics.All` without a resource (it enumerates the
      topics rather than pinning a count).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Mcp.UnitLibrary` and `Surl.Mcp` keep
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
