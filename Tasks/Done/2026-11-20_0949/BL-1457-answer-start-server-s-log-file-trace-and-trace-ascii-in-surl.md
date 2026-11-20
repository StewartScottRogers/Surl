---
id: BL-1457
title: Answer start_server's --log-file, --trace and --trace-ascii in surl-mcp's own terms
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1457 — Answer start_server's --log-file, --trace and --trace-ascii in surl-mcp's own terms

## Goal

`start_server` with `--log-file`, `--trace` or `--trace-ascii` in its `arguments` is refused with a
message an MCP caller can act on. It says surl-mcp writes each server's log to its own standard error,
and that `get_exchanges` and `get_events` show what a client sent. It no longer names
`SurlServerOptions.Log`.

## Context

- Surly bug report `2026-11-20T0553-Surl-bug-report.md` item 3, Low (Surl `763754785`, unchanged since).
- The text comes from `Application/Surl.Api/SurlServerArguments.cs:222-226` (`RefuseHostOwnedFiles`):
  "--log-file, --trace and --trace-ascii belong to a host; write the log to SurlServerOptions.Log." It
  is an `ArgumentException`, which `Application/Surl.Mcp.UnitLibrary/StartServerTool.cs:124-127` passes
  through as the tool's failure text.
- surl-mcp hands each server a `StartLogWriter` over its standard error (`StartServerTool.cs:85`,
  `StartLogWriter.cs`), and the `start_server` result keeps the start-up lines. The tool's schema
  (`:30-46`) has `listen_urls`, `arguments`, `files` and `files_base64`, and no log field.
- Fix it in surl-mcp, not in `Surl.Api`, whose message is right for a .NET caller: check `arguments` for
  the three options (long names, and `--log-file=<x>` forms) before starting, as `StartServerSafetyCheck`
  checks other words.

## Acceptance criteria

- [x] `start_server` with `arguments: ["--log-file", "x.log"]`, `["--trace", "t"]`,
      `["--trace-ascii=t"]` each returns an `isError` result whose text names the option, standard error,
      `get_exchanges` and `get_events`, and not `SurlServerOptions` (named tests in
      `Application/Surl.Mcp.UnitTests`).
- [x] No server is started for such a call (`list_servers` is empty afterwards).
- [x] `--help`, `--aihelp`, `--manual` and `--version` are still refused as before (existing tests
      green).
- [x] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` is clean, the fast tests are green,
      and coverage stays at 100% line and branch.

## Notes

Decision: refused in StartServerSafetyCheck (surl-mcp only), before parsing, so Surl.Api keeps its .NET-facing message. Shows as `refused: <option> is not accepted: ...`.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 3.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. start_server refuses --log-file/--trace/--trace-ascii in surl-mcp's own terms
