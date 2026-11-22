---
id: BL-1595
title: Prove surl-mcp's start_server answers mcp+stdio: with a tool error naming exit code 2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1594]
touches: [Application/Surl.Mcp.UnitLibrary/StartServerSafetyCheck.cs, Application/Surl.Mcp.UnitTests/StartServerToolTests.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-1595 — Prove surl-mcp's start_server answers mcp+stdio: with a tool error naming exit code 2

## Goal

`surl-mcp`'s `start_server` with `{"listen_urls": ["mcp+stdio:"]}` answers a tool result with `isError` true,
`exit_code` 2 and `exit_code_name` `FailedInit`, never a JSON-RPC `-32603` Internal error, and a test pins it.

## Context

Surly cookbook bug report 2026-11-22 (`Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T1116-Surl-bug-report.md`,
item 2), against Surl `5da5b3eaf`.

The cause, in today's code:
- `Application/Surl.Mcp.UnitLibrary/StartServerSafetyCheck.cs:42` parses the words with
  `ExtendedSchemeDefaultPorts.Schemes`, which has no `mcp+stdio` row (`ExtendedSchemeDefaultPorts.cs:36-37`), so
  the parse refuses `mcp+stdio:`, `CommandLine` is null and the check passes the start on to `Surl.Api` unchecked.
  The comment on line 41 ("Every extended scheme is accepted here, so the words parse whenever Surl.Api's narrower
  parse would") is therefore false for `mcp+stdio`.
- `Surl.Api` then throws `UriFormatException` (`Application/Surl.Api/SurlServer.cs:418`), which
  `StartServerTool.cs:115-128` does not catch (only `SurlServerStartException` and `ArgumentException`), so the MCP
  core answers -32603.

BL-1594 makes `SurlServer.StartAsync` throw `SurlServerStartException` (`FailedInit`) for `mcp+stdio:`, which
`StartServerTool.StartRefused` (`StartServerTool.cs:141-150`) already turns into the tool error wanted. This task
proves it end to end through the tool and corrects the comment; it adds no new refusal in the safety check (the
refusal is `Surl.Api`'s, so it stays one rule in one place).

Stand-in: none of store, events or faults apply; an agent that wants an MCP server it can observe uses
`mcp://127.0.0.1:0/` (ADR-0162 decision 1), which the refusal line names.

## Acceptance criteria

- [x] `StartServerToolTests.Handler_McpStdioListenUrl_IsAnErrorNamingExitCode2` (in `Application/Surl.Mcp.UnitTests`,
      `new McpTestHost(areExtensionsTurnedOn: true)`, called as `Handler_AnUnknownScheme_IsAnErrorNamingSurlsExitCode`
      is) passes: `IsError` is true, `exit_code` is 2, `exit_code_name` is `FailedInit`, the text starts with
      `surl: mcp+stdio:`, and `ListServers` lists no server afterwards.
- [x] The comment at `StartServerSafetyCheck.cs:41` says what the parse really accepts: every extended scheme with a
      default port, so `mcp+stdio:` is left to `Surl.Api`, which refuses it.
- [x] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` is clean and
      `dotnet test Application/Surl.Mcp.UnitTests --filter "TestCategory!=Integration"` passes; no test needs
      `TestCategory=Integration`.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
