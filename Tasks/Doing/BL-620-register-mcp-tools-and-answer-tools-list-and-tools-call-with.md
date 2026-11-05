---
id: BL-620
title: Register MCP tools and answer tools/list and tools/call with pagination in Surl.McpServing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-619]
touches: [Protocols/Http/Surl.McpServing.UnitLibrary, Protocols/Http/Surl.McpServing.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-620 — Register MCP tools and answer tools/list and tools/call with pagination in Surl.McpServing

## Goal

A caller of `Surl.McpServing` registers each tool with one explicit call - a name, a description, a hand-written
input schema and an async handler - and the core advertises the `tools` capability, lists the tools paged with
`cursor`/`nextCursor`, checks a call's arguments against the tool's schema, and answers `tools/call` with `content`
and `isError` for a tool failure and a JSON-RPC error for a protocol failure.

## Context

- Decision: BL-612's ADR, decision 1 (the tool model and registry, the page size, the cursor's form). Rules from the
  port's ADR-0801 that the ADR may adopt: a schema must be a JSON object whose `type` is `object`, a duplicate name
  is refused at registration. No assembly scanning, no attributes, no reflection (native AOT).
- This takes over the generic half of BL-465, which now registers the extended MCP server's own tools here.
- MCP "Server Features > Tools" (https://modelcontextprotocol.io/specification/2025-06-18/server/tools): the
  `tools/list` and `tools/call` examples, `inputSchema`, `isError`, and "Error Handling" (an unknown tool is
  `-32602`). "Utilities > Pagination": an opaque `cursor`; an invalid cursor is `-32602`.
- The handler gets the request's `CancellationToken`; an exception it throws becomes `isError: true` with its message
  or a `-32603`, as the ADR says.

## Acceptance criteria

- [ ] Named tests show: registering a duplicate name or a schema that is not an object schema refused; the
      `initialize` result advertising `tools` only when a tool is registered; `tools/list` with a page size smaller
      than the tool count returning `nextCursor`, and the next page returning the rest; an invalid cursor getting
      `-32602`; a call with good arguments returning the handler's `content`; a missing required argument and an
      argument of the wrong JSON type each getting `-32602`; an unknown tool getting `-32602`; a handler failure
      answered as the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
