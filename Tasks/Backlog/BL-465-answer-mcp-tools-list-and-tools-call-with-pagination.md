---
id: BL-465
title: Answer MCP tools/list and tools/call with pagination
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-464]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-465 — Answer MCP tools/list and tools/call with pagination

## Goal

The MCP server advertises the `tools` capability. It lists its tools with `inputSchema`, paged with
`cursor`/`nextCursor`, and calls them, returning `content` with `isError` for a tool failure and a JSON-RPC error
for a protocol failure, as MCP "Server Features > Tools" separates them.

## Context

- Decision: BL-463's ADR, decisions 2 and 7. The content store reaches the tools through `Surl.Content`
  (add its reference; ADR-0002's table allows it).
- MCP "Server Features > Tools" (the message examples, `isError`, and "Error Handling": an unknown tool is
  `-32602`) and "Utilities > Pagination" (an opaque `cursor`; an invalid cursor is `-32602`).
- Tool arguments are checked against each tool's `inputSchema`, so wrong arguments are `-32602`.

## Acceptance criteria

- [ ] Named tests show:
      - `tools/list` with a page size smaller than the tool count returning `nextCursor`, and the next page
        returning the rest;
      - an invalid cursor gets `-32602`;
      - each tool called with good arguments returning the specification's `content` shape;
      - a read of a missing file returning `isError: true`;
      - an unknown tool gets `-32602`;
      - missing arguments get `-32602`;
      - a write tool refused unless uploads are allowed, if the ADR serves one.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
