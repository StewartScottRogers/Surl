---
id: BL-1533
title: Name the 4 MiB message limit in the surl-mcp start_server and put_file tool descriptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1533 — Name the 4 MiB message limit in the surl-mcp start_server and put_file tool descriptions

## Goal

The `start_server` and `put_file` tool descriptions tell an agent that one MCP message is at most 4 MiB, so it seeds a large set of files with one `put_file` call per file instead of one oversized `start_server`.

## Context

Decided by BL-1522's amendment 1 to ADR-0103 (decision 3): the limit stays 4 MiB and the two descriptions name it. Descriptions live in `Application/Surl.Mcp.UnitLibrary/StartServerTool.cs` and `PutFileTool.cs` (`Description`). Surly cookbook bug report 2026-11-20, item 2. Stand-in: an agent seeding a stand-in must learn the limit before it hits it.

## Acceptance criteria

- [ ] `StartServerTool`'s description states that the whole `tools/call` message is at most 4 MiB (4194304 bytes) and points at `put_file` for larger seeds.
- [ ] `PutFileTool`'s description states the same limit applies to one file's text plus its JSON.
- [ ] A unit test pins each sentence; `dotnet build -warnaserror` and the fast tests of `Surl.Mcp.UnitTests` pass.
- [ ] Any `--aihelp` or README text that lists these tools' limits is updated in the same diff.

## Notes

## Log

- 2026-11-21: Created.
