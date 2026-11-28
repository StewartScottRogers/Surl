---
id: BL-2154
title: Keep the MCP input line reader reading messages at a message limit of int.MaxValue
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.McpServing.UnitLibrary, Protocols/Http/Surl.McpServing.UnitTests]
requirement: NFR-015
created: 2026-11-28
completed:
---
# BL-2154 — Keep the MCP input line reader reading messages at a message limit of int.MaxValue

## Goal

An MCP stdio session whose message limit is `int.MaxValue` reads its messages instead of discarding every one. This task changes nothing in `Documentation/Product/Requirements.md`; BL-2157 re-states NFR-014 to NFR-016.

## Context

Found while planning the `--max-message 0` fixes of BL-2120 (2026-11-28). In `Protocols/Http/Surl.McpServing.UnitLibrary/McpInputLineReader.cs`, `Append` discards a line when `_line.WrittenCount + bytes.Length > _maximumMessageBytes + 1`. At `int.MaxValue` the sum overflows `int` (the build checks no overflow), so every line is discarded as too long. Compare in `long`. `int.MaxValue` is what `surl` gives `mcp+stdio:` for `--max-message` 2147483647 or more, and what BL-2155 maps `--max-message 0` to. Check the library's other arithmetic on `MaximumMessageBytes` (`McpRequestDispatcher`) for the same fault. `McpSessionOptions` keeps its least value of 1. Stand-in: a host that turns the limit off through this library gets its messages answered; no store or event is involved.

## Acceptance criteria

- [ ] A test in `McpInputLineReaderTests` reads a message with a maximum of `int.MaxValue`.
- [ ] A test in `McpStreamSessionTests` with `McpSessionOptions` of `int.MaxValue` answers a request with its result, not `MessageTooLong`.
- [ ] The existing tests for a small maximum still discard a longer line.
- [ ] `dotnet build Protocols/Http/Surl.McpServing.UnitTests -warnaserror` is clean and the fast tests pass with no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
