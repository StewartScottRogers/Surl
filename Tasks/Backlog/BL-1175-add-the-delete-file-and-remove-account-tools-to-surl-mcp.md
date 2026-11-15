---
id: BL-1175
title: Add the delete_file and remove_account tools to Surl.Mcp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1154]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: FR-061
created: 2026-11-14
completed:
---
# BL-1175 — Add the delete_file and remove_account tools to Surl.Mcp

## Goal

`surl-mcp` offers the delete-file and remove-account tools BL-1154's ADR decides (and a clear-faults tool if it decides one), so an agent can undo `put_file` and `add_account` as `Surl.Api` and the control endpoint already can.

## Context

- `Application/Surl.Mcp.UnitLibrary`: model each tool on `PutFileTool.cs` and `AddAccountTool.cs`, over `RunningSurlServer`'s `Surl.Api` `SurlServer` (`Files.DeleteAsync`, `Accounts.Remove`, `Faults.Clear`). The tool list is built where the other tools are registered (follow `RemoveFaultTool.cs`'s registration).
- Rules (ADR-0103 decision 3, ADR-0124 decision 8): `server_id` required, `additionalProperties: false`, `structuredContent` equal to the text block, errors `isError: true`, never echo a password.

## Acceptance criteria

- [ ] `tools/list` lists the new tool(s) in the order BL-1154's ADR gives, with their schemas.
- [ ] Named tests: deleting a file `put_file` wrote makes it unreadable through `Surl.Api`; removing an account `add_account` added makes a login with it fail; each returns the ADR's result object.
- [ ] Named tests: an unknown `server_id`, a missing property, an extra property, an unknown path and an unknown account each give the ADR's error result.
- [ ] `dotnet build Surl.slnx -warnaserror` is clean, the fast tests are green, and `Surl.Mcp.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
