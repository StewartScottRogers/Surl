---
id: BL-1201
title: Add Surl.Mcp's read-back tools for mailbox messages, files and bus messages
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1199]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: FR-061
created: 2026-11-15
completed:
---
# BL-1201 — Add Surl.Mcp's read-back tools for mailbox messages, files and bus messages

## Goal

An AI agent connected to `surl-mcp` reads back a running server's mailbox messages, files and bus messages with the tools BL-1194's ADR names.

## Context

- Decision: the ADR filed by BL-1194 (tool names, input properties, results, bounds). `Surl.Api` members: BL-1199.
- Where: `Application/Surl.Mcp.UnitLibrary`. Follow `PutFileTool.cs`, `SeedMailTool.cs` and `PublishMessageTool.cs` for a `server_id` tool (`RunningServerTool`), `additionalProperties: false`, `structuredContent` beside the same object as text (ADR-0103 decision 3), and `isError: true` for an unknown server ID. Bytes are returned as text or base64 by ADR-0103 decision 3's rule. Register the tools in `tools/list` in the order the ADR gives.
- Safety (ADR-0103 decision 4): no local path is read outside the server's store, and no secret is returned.

## Acceptance criteria

- [ ] Each tool the ADR names exists with its input schema, and `tools/list` lists it in the ADR's order.
- [ ] Tests in `Application/Surl.Mcp.UnitTests` start a server through `start_server`, seed it through `seed_mail`, `put_file` and `publish_message`, and read the same bytes back through the new tools.
- [ ] A test shows an unknown `server_id` gives `isError: true`, and a test shows a not-found item gives the ADR's result.
- [ ] A test shows binary content comes back as base64 and UTF-8 text as text.
- [ ] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` is clean, the fast tests pass, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
