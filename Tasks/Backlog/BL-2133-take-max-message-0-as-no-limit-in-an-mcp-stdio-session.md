---
id: BL-2133
title: Take --max-message 0 as no limit in an mcp+stdio: session
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2132]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: NFR-015
created: 2026-11-28
completed:
---
# BL-2133 — Take --max-message 0 as no limit in an mcp+stdio: session

## Goal

`surl --max-message 0 mcp+stdio:` serves its session, reading messages and files with no message limit, instead of failing to build the session. This task changes nothing in `Documentation/Product/Requirements.md`; BL-2135 re-states NFR-014 to NFR-016.

## Context

Found while working BL-2120 (2026-11-28). `Application/Surl.Composition.UnitLibrary/McpStdioComposition.cs` (`ServeAsync`) passes `(int)Math.Min(commandLine.Limits.MaxMessageBytes, int.MaxValue)` to `McpStdioSession.RunAsync`. With 0, `McpSessionOptions` throws `ArgumentOutOfRangeException` (it needs at least 1), and `McpReadFileTool` would refuse every answer. Pass `int.MaxValue` for 0, as `McpHttpRequestAnswerer` does for the HTTP transport. BL-2132 makes the line reader work at that value. ADR-0111's limits table bounds a stdio line and a `read_file` answer by `--max-message`. Stand-in: a test that runs `mcp+stdio:` with the limit off can read a large seeded file; the session is outside the event stream and the fault rules (ADR-0111), so nothing new is published.

## Acceptance criteria

- [ ] A test in `McpStdioCompositionTests` with `--max-message 0` runs `initialize` to completion over in-memory streams.
- [ ] A test in `McpStdioCompositionTests` with `--max-message 0` accepts a request line longer than 1048576 bytes and answers it.
- [ ] A test with a small non-zero `--max-message` still answers `MessageTooLong` for a longer line.
- [ ] `dotnet build Application/Surl.Composition.UnitTests -warnaserror` is clean and the fast tests pass with no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
