---
id: BL-2105
title: Say that a restore drops the messages of queues exclusive to a connection
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Application/Surl.Api]
requirement: none
created: 2026-11-28
completed:
---
# BL-2105 — Say that a restore drops the messages of queues exclusive to a connection

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

- [ ] `RestoreServerTool.Description` says a restore drops the messages of queues exclusive to a connection and keeps the queues, and a test in `Application/Surl.Mcp.UnitTests` asserts it.
- [ ] `Application/Surl.Mcp.UnitLibrary/CLAUDE.md`'s `restore_server` entry and `SurlServer.RestoreAsync`'s XML documentation say the same.

## Notes

## Log

- 2026-11-28: Created.
