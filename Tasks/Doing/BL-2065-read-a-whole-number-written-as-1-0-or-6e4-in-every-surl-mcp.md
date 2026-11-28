---
id: BL-2065
title: Read a whole number written as 1.0 or 6e4 in every surl-mcp integer argument
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: FR-061
created: 2026-11-28
completed:
---
# BL-2065 — Read a whole number written as 1.0 or 6e4 in every surl-mcp integer argument

## Goal

Every `surl-mcp` tool reads an integer argument its input schema accepts, instead of answering `-32603 Internal error`.

## Context

`McpToolInputSchema` counts a number with no fractional part (`1.0`, `6e4`) as an integer, but `JsonElement.GetInt32` and `GetInt64` throw `FormatException` for one. BL-2032 fixed `RedisKeyArguments` (`set_key`, `start_server`'s `redis_keys`) by reading with `GetDouble`. The same calls remain in `BrowseQueueTool`, `EventToolFilter`, `GetEventsTool`, `GetExchangesTool`, `ListFilesTool`, `ListKeysTool`, `ListMessagesTool`, `ReadKeyTool`, `ReadLogTool`, `ReadMessageTool` and `WaitForEventTool`. Some of their arguments have no schema `maximum` (or are range-checked by the tool, as `timeout_ms` is), so a value too large for the type must be refused or clamped by decision, not cast blindly.

## Acceptance criteria

- [ ] Named test in `Surl.Mcp.UnitTests`: each integer argument of each tool above, written as `1.0`, is read as 1.
- [ ] Named test: a whole number too large for the argument's type is answered with an error naming the argument, not `-32603`.
- [ ] `Select-String -Path Application\Surl.Mcp.UnitLibrary\*.cs -Pattern 'GetInt32\(\)|GetInt64\(\)'` finds nothing.
- [ ] The fast tests pass and `Surl.Mcp.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
