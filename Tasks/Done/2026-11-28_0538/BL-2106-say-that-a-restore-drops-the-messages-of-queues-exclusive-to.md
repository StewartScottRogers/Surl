---
id: BL-2106
title: Say that a restore drops the messages of queues exclusive to a connection
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Application/Surl.Api]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2106 — Say that a restore drops the messages of queues exclusive to a connection

## Goal

Someone reading the `restore_server` description or `SurlServer.RestoreAsync`'s documentation learns what a restore
does to a queue exclusive to a connection.

## Context

Found while working BL-2099, read from source on 2026-11-28. A snapshot copies no queue exclusive to a connection
(`MessageBus.TakeSnapshot`, `Protocols/Messaging/Surl.MessageBus.UnitLibrary/MessageBus.SeedResetSnapshot.cs`).
`MessageBus.Restore` drops every queue's messages and keeps an exclusive queue itself
(`EmptyQueuesAndRemoveUnusedOnes`), so an exclusive queue stays and its messages are lost.
`RestoreServerTool.Description`, its entry in `Application/Surl.Mcp.UnitLibrary/CLAUDE.md` and the list of what a
restore clears in `Application/Surl.Api/SurlServer.cs` (`RestoreAsync`) do not say so.

## Acceptance criteria

- [x] `RestoreServerTool.Description` says a restore drops the messages of queues exclusive to a connection and keeps the queues, and a test in `Application/Surl.Mcp.UnitTests` asserts it.
- [x] `Application/Surl.Mcp.UnitLibrary/CLAUDE.md`'s `restore_server` entry and `SurlServer.RestoreAsync`'s XML documentation say the same.

## Notes

- Also said it in `RestoreServerTool`'s class comment and in the `Messages` row of `Application/Surl.Api/README.md`'s
  reset and restore table, which read as if a restore left exactly the snapshot's queues.
- Same-name case: today a restore whose snapshot holds a queue of an exclusive queue's name drops that queue's own
  messages and puts the snapshot's into it. BL-2095 (in Doing) changes that case to replace the queue; a queue of
  that name stays either way, so these sentences need no change for it.
- Verified: build with `-warnaserror` clean, `dotnet format --verify-no-changes` exit 0, fast tests all green.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. restore_server's description, SurlServer.RestoreAsync's documentation, the MCP library notes and the API README say a restore drops the messages of queues exclusive to a connection and keeps the queues
