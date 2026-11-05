---
id: BL-621
title: Serve an MCP session over newline-delimited stdio streams in Surl.McpServing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-620]
touches: [Protocols/Http/Surl.McpServing.UnitLibrary, Protocols/Http/Surl.McpServing.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-621 — Serve an MCP session over newline-delimited stdio streams in Surl.McpServing

## Goal

`Surl.McpServing` serves one MCP session over a pair of byte streams given by its caller: it reads
newline-delimited JSON-RPC messages from one, writes only MCP messages, each on one line, to the other, answers
requests concurrently as the ADR allows, honours `notifications/cancelled`, and ends cleanly when its input closes.
Both the `Surl.Mcp` host and the extended MCP server's `mcp+stdio` bind it to real standard streams.

## Context

- Decision: BL-612's ADR, decisions 1 and 5 (the stream-pair seam's shape, the line limit, concurrency, and what
  ending the input does). This takes over the generic half of BL-467.
- MCP "Transports > stdio" (https://modelcontextprotocol.io/specification/2025-06-18/basic/transports): messages are
  delimited by newlines and must not contain embedded newlines; the server must not write anything to its output
  that is not a valid MCP message, and may log to standard error. "Lifecycle > Shutdown": the client closes the
  server's input. "Utilities > Cancellation": `notifications/cancelled` with the `requestId`.
- The library never touches `Console`; it takes `Stream`s (or the pipe types the ADR names). Tests use in-memory
  streams, `.claude/rules/testing.md`.

## Acceptance criteria

- [ ] Named tests over in-memory streams show: an `initialize`, `tools/list` and `tools/call` exchange with each
      response on exactly one line; a JSON string containing `\n` written escaped; nothing but MCP messages written
      to the output; the session returning when the input closes; a line over the limit refused as the ADR says;
      a `notifications/cancelled` for a running call cancelling that handler's token.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
