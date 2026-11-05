---
id: BL-467
title: Serve MCP over the stdio transport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-465, BL-621]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-467 — Serve MCP over the stdio transport

## Goal

The MCP server serves one session over a pair of byte streams through the shared core's newline-delimited stdio
session (`Surl.McpServing`, BL-621), with this server's lifecycle and tools, and ends when its input closes. The
executable can then bind those streams to its own standard input and output.

## Context

- Decision: BL-463's ADR, decision 5.
- Amended 2026-11-04: the newline framing, the line limit, escaping, "nothing but messages on the output" and ending
  on a closed input are `Surl.McpServing`'s (BL-621), shared with the `Surl.Mcp` host, and tested there. This task
  exposes this server's session over that core seam, in the shape BL-468 binds to standard streams.
- MCP "Transports > stdio" and "Lifecycle > Shutdown" (stdio): the client closes the server's input.
- The library takes the streams from its caller (an `IConnection`, or the stream pair BL-612's ADR names), and never
  touches `Console`. The executable binds them in BL-468.

## Acceptance criteria

- [ ] Named tests show, over in-memory streams:
      - an `initialize`, `tools/list` and `tools/call` exchange with this server's tools, each response on one line;
      - the session ending cleanly when the input closes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended by task-planner: depends on BL-621, whose shared core now owns stdio framing and its limits;
  this task binds the server's session to it.
- 2026-11-05: Backlog -> Doing.
