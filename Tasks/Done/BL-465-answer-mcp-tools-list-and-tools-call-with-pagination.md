---
id: BL-465
title: Serve the MCP server's tools through Surl.McpServing's tools/list and tools/call
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-464, BL-620]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests, Protocols/Http/Surl.McpServing.UnitLibrary, Protocols/Http/Surl.McpServing.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] Named tests show:
      - `tools/list` through this server's session listing exactly the ADR's tools, each with its schema;
      - each tool called with good arguments returning the specification's `content` shape;
      - a read of a missing file returning `isError: true`;
      - a path outside the content store refused as the content store refuses it;
      - a write tool refused unless uploads are allowed, if the ADR serves one.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- **Touches widened** (2026-11-05): `Surl.McpServing` and its tests. ADR-0111 decision 2 returns a binary file
  as an embedded `resource` block, and the core's `McpToolResult` wrote text blocks only. No task in Doing
  touched `Surl.McpServing`, so this task added `McpEmbeddedResource` and an `McpToolResult` constructor taking
  them; `McpToolMethods` writes them after the text blocks as `{"type":"resource","resource":{"uri","mimeType","blob"}}`
  ("Server Features > Tools", "Embedded Resources").
- **One registry per session, not per server** (decided by Claude under Stewart's delegation): a tool handler
  receives only its arguments and token, and every file operation must run as the session's `ContentCaller`, so
  `McpServerTools.CreateRegistry(store, caller, maximumMessageBytes)` is called once per session (four `AddTool`
  calls, cheap). The alternative, an `AsyncLocal` caller, would be ambient state the handler's signature hides.
  BL-466/BL-467 pass the caller they authenticated.
- **Paths**: one leading `/` dropped; each segment percent-encoded (`Uri.EscapeDataString`) before
  `MapRequestPathForCaller`, so `%` and spaces are literal characters and `\`, `:`, `..`, `//`, device names
  reach the store's own refusals (`the path is refused`). A dot-file, `.surl` and a share the caller may not read
  are answered `no such file or folder`, because the content store answers them as absent - ADR-0111's table puts
  dot-files under "refused"; the store's rule wins ("the store's own rules apply unchanged").
- **Added texts** the ADR left open: `the path is a folder, not a file` (read of a folder), `the path is a file,
  not a folder` (listing of a file), `base64 is not valid base64`.
- **`--max-filesize`** is enforced by `ContentStore.WriteUploadAsync` (`TooLarge`): it writes to a temporary file
  and deletes it, so the target is never touched; no separate pre-check, so no unreachable branch.
- **Message limit on reads**: refused when the file's length passes the limit (without reading it), or when the
  JSON-escaped text (`JsonEncodedText`, the same default encoder `Utf8JsonWriter` uses) or the base64 does.
- Titles chosen: `List a folder`, `Read a file`, `Write a file`, `Echo`.
- Tests: `McpServerToolsTests` (tools/list exactly the four in order, each schema `DeepEquals` its constant;
  default server lists `read_file` and `echo` only), `McpReadFileToolTests`, `McpListDirectoryToolTests`,
  `McpWriteFileToolTests`, `McpEchoToolTests`; core: `McpEmbeddedResourceTests`, new cases in
  `McpToolResultTests` and `McpToolMethodsTests`. McpServer.UnitTests 59, McpServing.UnitTests 217.
- Coverage (`Measure-CodeQuality.ps1 -SkipTestRun`): both libraries 100% line and branch. The core has one
  pre-existing complexity-12 member, `McpStreamSession.CancelRequest`, untouched here: filed **BL-746**.
- Follow-up **BL-745**: amend ADR-0111 decisions 1 and 2 to the above (its folder was held by BL-441).

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended by task-planner: depends on BL-620, whose shared core now owns the tool registry, pagination
  and argument checks; this task keeps only the server's own tools. Title changed to match.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The MCP server registers list_directory, read_file, write_file and echo with Surl.McpServing, reading and writing through Surl.Content as the session's caller
