---
id: BL-465
title: Serve the MCP server's tools through Surl.McpServing's tools/list and tools/call
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-464, BL-620]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-465 — Serve the MCP server's tools through Surl.McpServing's tools/list and tools/call

## Goal

The MCP server registers the tools BL-463's ADR decides - each with a hand-written description and `inputSchema` -
with the shared core's tool registry (BL-620), so it advertises `tools`, lists them paged and answers calls,
returning `content` with `isError` for a tool failure, as MCP "Server Features > Tools" separates them.

## Context

- Decision: BL-463's ADR, decisions 2 and 7. The content store reaches the tools through `Surl.Content`
  (add its reference; ADR-0002's table allows it).
- Amended 2026-11-04: the registry, pagination, argument checking against `inputSchema`, and the `-32602` answers
  for an unknown tool, an invalid cursor and bad arguments are `Surl.McpServing`'s (BL-620), shared with the
  `Surl.Mcp` host, and tested there. This task writes this server's tools and registers each with one line.
- MCP "Server Features > Tools" (the message examples, `isError`).

## Acceptance criteria

- [ ] Named tests show:
      - `tools/list` through this server's session listing exactly the ADR's tools, each with its schema;
      - each tool called with good arguments returning the specification's `content` shape;
      - a read of a missing file returning `isError: true`;
      - a path outside the content store refused as the content store refuses it;
      - a write tool refused unless uploads are allowed, if the ADR serves one.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended by task-planner: depends on BL-620, whose shared core now owns the tool registry, pagination
  and argument checks; this task keeps only the server's own tools. Title changed to match.
- 2026-11-05: Backlog -> Doing.
