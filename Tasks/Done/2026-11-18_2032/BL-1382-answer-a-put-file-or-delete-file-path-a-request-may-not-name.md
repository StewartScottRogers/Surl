---
id: BL-1382
title: Answer a put_file or delete_file path a request may not name with an isError tool result
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-1382 — Answer a put_file or delete_file path a request may not name with an isError tool result

## Goal

`surl-mcp`'s `put_file` and `delete_file` answer a `path` a request may not name - relative (`reports/b.txt`) or
holding a `..` segment (`/../x.txt`) - with a tool result whose `isError` is true and whose text names the path
and why, instead of the JSON-RPC error `-32603 Internal error`.

## Context

- Finding 5 of the Surly cookbook bug report of 2026-11-18: after `start_server` with
  `{"listen_urls":["http://127.0.0.1:0"]}`, `put_file` with `{"server_id":"s1","path":"reports/b.txt","text":"x"}`
  or `"path":"/../x.txt"` answers `{"code": -32603, "message": "Internal error"}`.
- Cause: `ContentStore.MapForTheTest` (`Foundation/Surl.Content.UnitLibrary/ContentStore.Control.cs:252-258`)
  throws `ArgumentException("The request path is not one a request may name (<refusal>).")` for such a path; it is
  called by `SeedFileAsync` (line 67) and the delete at line 136. `Surl.Api`'s `SurlServerFiles.PutAsync` and
  `DeleteAsync` (`Application/Surl.Api/SurlServerFiles.cs:35-62`) let it through, and
  `RunningServerTool.ActAsync` (`Application/Surl.Mcp.UnitLibrary/RunningServerTool.cs:54-61`) catches only
  `InvalidOperationException`, so the `ArgumentException` reaches the MCP session as an internal error.
- Other tools already turn bad input into `isError` results with their own text: `AddAccountTool.cs:55`,
  `StartServerTool.cs:124` (`catch (ArgumentException ...)`), and `DeleteFileTool.cs:49-51` (`no file at <path>`).
  Fix it in the two tools (`PutFileTool.cs:51`, `DeleteFileTool.cs:47`), not in `RunningServerTool`, so an
  `ArgumentException` that is a real bug elsewhere still surfaces.
- Stand-in: no store change, event or fault is involved; an agent seeding a stand-in learns what it did wrong.

## Acceptance criteria

- [x] `put_file` with `"path": "reports/b.txt"` and with `"path": "/../x.txt"` each answers a tool result with
      `isError` true whose text contains the path as given and the refusal reason, and the store is unchanged.
- [x] `delete_file` with the same two paths answers the same way.
- [x] Each case is a named test in `Application/Surl.Mcp.UnitTests` (`StoreToolsTests` for `put_file`,
      `DeleteAndRemoveToolsTests` for `delete_file`), run over `McpTestHost` with no network.
- [x] `PutFileTool`'s and `DeleteFileTool`'s XML docs and the `put_file`/`delete_file` lines of
      `Application/Surl.Mcp.UnitLibrary/CLAUDE.md` name the new `isError` text.
- [x] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Mcp.UnitLibrary` stays at 100% line and branch coverage.

## Notes

- `Surl.Api`'s `PutAsync` and `DeleteAsync` XML docs do not list the `ArgumentException`; BL-1383 adds it.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing.
- 2026-11-18: Doing -> Done. put_file and delete_file answer an unnameable path with an isError result naming the path and reason
