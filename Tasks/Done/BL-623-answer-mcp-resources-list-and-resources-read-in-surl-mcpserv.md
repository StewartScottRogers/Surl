---
id: BL-623
title: Answer MCP resources/list and resources/read in Surl.McpServing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-620]
touches: [Protocols/Http/Surl.McpServing.UnitLibrary, Protocols/Http/Surl.McpServing.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-623 — Answer MCP resources/list and resources/read in Surl.McpServing

## Goal

A caller of `Surl.McpServing` registers resource providers with one explicit call each, and the core advertises the
`resources` capability, lists the resources paged with `cursor`/`nextCursor`, and reads one by URI - so `Surl.Mcp`
can serve the `surl --aihelp` topics as resources.

## Context

- Decision: BL-612's ADR, decisions 1 and 6 (the resource model, and that the `--aihelp` topics are served as
  resources). The port's ADR-0801 decision 4 (a provider lists a fixed set; a URI registered twice is refused and
  none of that provider's resources is added) is copyable; the ADR says whether Surl adopts it.
- MCP "Server Features > Resources" (https://modelcontextprotocol.io/specification/2025-06-18/server/resources): the
  `resources/list` and `resources/read` examples, `uri`, `name`, `mimeType`, `text`, and "Error Handling" (resource
  not found is `-32002`; an internal error `-32603`).
- Pagination: the same cursor handling as `tools/list` (BL-620).

## Acceptance criteria

- [x] Named tests show: the `initialize` result advertising `resources` only when a provider is registered; a URI
      registered twice refused; `resources/list` paged with `nextCursor`; `resources/read` of a listed URI returning
      its `contents` in the page's shape; an unknown URI getting `-32002`; missing `uri` getting `-32602`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- No new ADR: ADR-0103 decision 1 already adopts the port's ADR-0801 decision 4 (a fixed list read once; a
  URI already registered or listed twice refuses the whole provider; a null read is `-32002`; no templates).
- Added `McpResource`, `IMcpResourceProvider` (`Resources`, `ReadTextAsync` returning null for not found),
  `McpServerRegistry.AddResources`/`Resources`, and `McpResourceMethods`. `JsonRpcError` gained an optional
  `ResourceUri`, written as `data.uri`, set only by `JsonRpcError.ResourceNotFound` (the spec's error example).
- Defaults taken: list entries and read contents follow the spec page's example field order (uri, name,
  title, description, mimeType / uri, name, title, mimeType, text); `title` only for `2025-06-18`, as for
  tools; optional fields omitted when null. A provider that throws is `-32603` and a read cancelled through
  its token gets no response - the same rules as `tools/call`, so the two methods behave alike.
- Tests: `McpResourceMethodsTests` (the page's list, read and not-found examples; paging; `-32602 uri`;
  capability on and off), `McpServerRegistryTests.AddResources_*`, `McpResourceTests`. The library is at 100%
  line and branch coverage; Surl.McpServing.UnitTests runs 244 tests.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.McpServing registers resource providers and answers resources/list (paged) and resources/read, advertising resources only once one is added
