---
id: BL-1201
title: Add Surl.Mcp's read-back tools for mailbox messages, files and bus messages
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1199]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Application/Surl.Mcp]
requirement: FR-061
created: 2026-11-15
completed: 2026-11-15
---
# BL-1201 — Add Surl.Mcp's read-back tools for mailbox messages, files and bus messages

## Goal

An AI agent connected to `surl-mcp` reads back a running server's mailbox messages, files and bus messages with the tools BL-1194's ADR names.

## Context

- Decision: the ADR filed by BL-1194 (tool names, input properties, results, bounds). `Surl.Api` members: BL-1199.
- Where: `Application/Surl.Mcp.UnitLibrary`. Follow `PutFileTool.cs`, `SeedMailTool.cs` and `PublishMessageTool.cs` for a `server_id` tool (`RunningServerTool`), `additionalProperties: false`, `structuredContent` beside the same object as text (ADR-0103 decision 3), and `isError: true` for an unknown server ID. Bytes are returned as text or base64 by ADR-0103 decision 3's rule. Register the tools in `tools/list` in the order the ADR gives.
- Safety (ADR-0103 decision 4): no local path is read outside the server's store, and no secret is returned.

## Acceptance criteria

- [x] Each tool the ADR names exists with its input schema, and `tools/list` lists it in the ADR's order.
- [x] Tests in `Application/Surl.Mcp.UnitTests` start a server through `start_server`, seed it through `seed_mail`, `put_file` and `publish_message`, and read the same bytes back through the new tools.
- [x] A test shows an unknown `server_id` gives `isError: true`, and a test shows a not-found item gives the ADR's result.
- [x] A test shows binary content comes back as base64 and UTF-8 text as text.
- [x] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` is clean, the fast tests pass, and coverage stays at 100% line and branch.

## Notes

- `Application/Surl.Mcp` added to `touches`: `Program.cs` registers each tool with one `AddTool` line, and no other task in Doing names it.
- Not-found and refused reads (absent owner, mailbox, UID, file, queue, log; bound; bad path) are the API's `InvalidOperationException` subclasses, so `RunningServerTool` already gives `isError: true` with the message, as ADR-0164 decision 6 says. An absent retained message is `{"retained": false}`, not an error.
- A log record's key is `key` (text) or `key_base64`, since `base64` is the payload's. Record and queue-message writers are internal methods so tests reach the header and key branches the seeding tools cannot produce.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Eight read-back tools added to surl-mcp; build clean, fast tests green, 100% coverage
