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
completed: 2026-11-05
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

- [x] Named tests show: registering a duplicate name or a schema that is not an object schema refused; the
      `initialize` result advertising `tools` only when a tool is registered; `tools/list` with a page size smaller
      than the tool count returning `nextCursor`, and the next page returning the rest; an invalid cursor getting
      `-32602`; a call with good arguments returning the handler's `content`; a missing required argument and an
      argument of the wrong JSON type each getting `-32602`; an unknown tool getting `-32602`; a handler failure
      answered as the ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Delivered against ADR-0103 decision 1 (no new ADR: every choice below sits inside it).

- `McpTool` (name, title, description, schema text, handler), `McpServerRegistry.AddTool`, `McpToolResult`
  (text blocks, optional `structuredContent`, `isError`; `FromText`/`FromFailure`), `McpToolFailureException`,
  and the internal `McpToolInputSchema`, `McpListCursor`, `McpToolMethods`, `McpRegisteredTool`.
- `McpRequestDispatcher` now takes `(McpServerRegistry, McpSessionOptions)` and answers with
  `AnswerAsync(message, cancellationToken)`, since `tools/call` awaits its handler. The capabilities come from
  the registry, as the ADR says ("the capabilities the registry earns"), so `McpServerCapabilities` and
  `McpSessionOptions.Capabilities` are gone; BL-623 adds `resources` to the registry the same way. Default
  taken: the caller-set page size lives on `McpSessionOptions.ListPageSize` (100) beside the message limit.
- Default taken: `tools/list` and `tools/call` are answered whether or not a tool is registered (an empty list,
  or `unknown tool`), rather than `-32601`: one code path, and harmless.
- Wording of `-32602` messages (the ADR fixes only the `Invalid params: <which>` shape): `name` for a missing or
  non-string tool name, `unknown tool <name>`, `cursor`, and `arguments.<path>: required | not allowed |
  expected a string | expected one of a, b | expected at least 1 | expected at most 3 characters ...`.
  `"arguments": null` counts as missing (the empty object). `additionalProperties` accepts only a boolean.
- A handler's `OperationCanceledException` gets no response only when the request's token was cancelled;
  thrown otherwise it is `-32603`, like any other exception (ADR: "a cancellation" through its token).
- `inputSchema` is listed as the parsed JSON re-written, so a schema typed on several lines still goes out as
  one line.
- Fixed while here: `McpRequestDispatcher.TryReadInitializeParams` (from BL-619) measured complexity 12; split.
- Coverage (`Measure-CodeQuality.ps1 -Library Surl.McpServing.UnitLibrary`): 100% line, 100% branch, 111
  members, worst CRAP 10.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.McpServing registers tools with AddTool, advertises tools, pages tools/list by cursor and answers tools/call with schema-checked arguments, isError and JSON-RPC errors
