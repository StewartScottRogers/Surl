---
id: BL-2065
title: Read a whole number written as 1.0 or 6e4 in every surl-mcp integer argument
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Documentation/Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md]
requirement: FR-061
created: 2026-11-28
completed: 2026-11-28
---
# BL-2065 — Read a whole number written as 1.0 or 6e4 in every surl-mcp integer argument

## Goal

Every `surl-mcp` tool reads an integer argument its input schema accepts, instead of answering `-32603 Internal error`.

## Context

`McpToolInputSchema` counts a number with no fractional part (`1.0`, `6e4`) as an integer, but `JsonElement.GetInt32` and `GetInt64` throw `FormatException` for one. BL-2032 fixed `RedisKeyArguments` (`set_key`, `start_server`'s `redis_keys`) by reading with `GetDouble`. The same calls remain in `BrowseQueueTool`, `EventToolFilter`, `GetEventsTool`, `GetExchangesTool`, `ListFilesTool`, `ListKeysTool`, `ListMessagesTool`, `ReadKeyTool`, `ReadLogTool`, `ReadMessageTool` and `WaitForEventTool`. Some of their arguments have no schema `maximum` (or are range-checked by the tool, as `timeout_ms` is), so a value too large for the type must be refused or clamped by decision, not cast blindly.

## Acceptance criteria

- [x] Named test in `Surl.Mcp.UnitTests`: each integer argument of each tool above, written as `1.0`, is read as 1.
- [x] Named test: a whole number too large for the argument's type is answered with an error naming the argument, not `-32603`.
- [x] `Select-String -Path Application\Surl.Mcp.UnitLibrary\*.cs -Pattern 'GetInt32\(\)|GetInt64\(\)'` finds nothing.
- [x] The fast tests pass and `Surl.Mcp.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Decision (ADR-0103 amendment 7): refuse, never clamp. Each integer argument with no bound took a schema `maximum`
  (`after_position` 2147483647; the `long` ones 2^53 - 1, the largest bound the schema's double comparison holds
  exactly), so a larger number is `-32602` naming it. `timeout_ms` stays range-checked by the tool, which now shows
  the number as the caller wrote it.
- All reads go through `JsonElementWholeNumbers`; `RedisKeyArguments` uses it too.
- Added ADR-0103's file to `touches` to record the decision; no task in Doing names it.
- Tests: `JsonElementWholeNumbersTests.EveryToolsIntegerArgument_WrittenAsOnePointZero_IsReadAsOne` and
  `WaitForEventsTimeout_WrittenAsOnePointZero_IsOneMillisecond`;
  `ProgramTests.RunAsync_WholeNumberTooLargeForItsArgument_IsRefusedNamingTheArgument` and
  `WaitForEventToolTests.Handler_TimeoutTooLargeForALong_IsAnErrorNamingTheArgument`.
- 2026-11-28: build 0 warnings, format clean, fast tests 30734 passed, 0 failed, 3 skipped;
  `Surl.Mcp.UnitLibrary` 100% line, 100% branch, worst CRAP 7. No Integration run: no socket or disk code changed.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. Every surl-mcp tool reads 1.0 or 6e4 as an integer, and a number too large for an argument is refused naming it
