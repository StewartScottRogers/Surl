---
id: BL-1523
title: Answer an over-limit MCP message with its own request id per BL-1522's ADR-0103 amendment
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1522]
touches: [Surl.McpServing.UnitLibrary, Surl.McpServing.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1523 — Answer an over-limit MCP message with its own request id per BL-1522's ADR-0103 amendment

## Goal

A message longer than the session's limit is answered with the refusal BL-1522's amendment to ADR-0103 decides - carrying the request's own `id` when it can be read - so a `surl-mcp` client's oversized `tools/call` (such as `start_server` with 5 MB of `files`) gets an answer instead of waiting for ever.

## Context

Surly cookbook bug report 2026-11-20, item 2. Read BL-1522's amendment first; it decides the refusal text and how the `id` is found. If it also changes `surl-mcp`'s limit or tool descriptions, those are its other tasks (in this task's `depends-on` only if they must land first).

The code, in `Protocols/Http/Surl.McpServing.UnitLibrary`:
- `McpInputLineReader.cs:81-93` (`Append`) stops buffering at the limit and sets `_discarding`; `EndLine` (lines 100-106) returns `[]` for a discarded line. The `id` scan belongs here, fed the bytes `Append` sees while discarding, so nothing past the limit is kept.
- `McpStreamSession.cs`, `AnswerAsync`: an empty message is answered with `_dispatcher.AnswerMessageTooLong()`.
- `McpRequestDispatcher.cs:72-73` (`AnswerMessageTooLong`) writes `JsonRpcError.MessageTooLong` with a null `id` through `JsonRpcResponseWriter.WriteError`; `JsonRpcRequestId` is the `id` type.

The library is the one `surl-mcp` (`Application/Surl.Mcp/Program.cs`) and the extended MCP server share, so both change together; neither host's code needs editing.

Stand-in: no store or event; the observable is the response line on the session's output.

## Acceptance criteria

- [ ] An `McpStreamSessionTests` test: a `tools/call` line of 5,000,000 bytes with `"id":7` first is answered by one `-32600` response with `"id":7` and the amendment's text, and the next request on the session is answered.
- [ ] The same with the `id` (a string id, e.g. `"id":"abc"`) as the last member, after the oversized `params`.
- [ ] A test that an over-long line whose `id` cannot be read (none, nested only, or malformed) is answered as `RunAsync_LineOverTheLimit_AnswersInvalidRequestWithANullIdAndContinues` pins today, unless the amendment says otherwise.
- [ ] `McpInputLineReaderTests` tests show the reader keeps no more than the limit plus the amendment's bound for the `id` while discarding, across reads that split the `id` and a string holding `\"id\":` escapes.
- [ ] `dotnet build Protocols/Http/Surl.McpServing.UnitLibrary -warnaserror` is clean and `dotnet test Protocols/Http/Surl.McpServing.UnitTests --filter "TestCategory!=Integration"` passes, with 100% line and branch coverage of the changed files and no method past cyclomatic complexity 10.
- [ ] `Protocols/Http/Surl.McpServing.UnitLibrary/CLAUDE.md` states how an over-limit message is answered.

## Notes

## Log

- 2026-11-20: Created.
