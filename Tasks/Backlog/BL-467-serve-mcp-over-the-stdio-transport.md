---
id: BL-467
title: Serve MCP over the stdio transport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-465]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-467 — Serve MCP over the stdio transport

## Goal

The MCP server serves one session over a pair of byte streams. It reads newline-delimited JSON-RPC messages from
one stream, writes only MCP messages, each on its own line, to the other, and ends when its input closes. The
executable can then bind those streams to its own standard input and output.

## Context

- Decision: BL-463's ADR, decision 5.
- MCP "Transports > stdio": messages are delimited by newlines and must not contain embedded newlines. The
  server must not write anything to stdout that is not a valid MCP message, and may log to stderr. "Lifecycle >
  Shutdown" (stdio): the client closes the server's input.
- The library takes the streams from its caller (an `IConnection`, or a stream pair the ADR names), and never
  touches `Console`. The executable binds them in BL-468.

## Acceptance criteria

- [ ] Named tests show:
      - an `initialize`, `tools/list` and `tools/call` exchange over in-memory streams, each response on one line
        with no embedded newline;
      - a JSON string containing `\n` written escaped;
      - nothing but messages written to the output;
      - the session ending cleanly when the input closes;
      - a line over the limit refused as the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
