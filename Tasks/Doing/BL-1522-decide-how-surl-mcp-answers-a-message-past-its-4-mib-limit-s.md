---
id: BL-1522
title: Decide how surl-mcp answers a message past its 4 MiB limit so the client's request is not left unanswered
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-20
completed:
---
# BL-1522 — Decide how surl-mcp answers a message past its 4 MiB limit so the client's request is not left unanswered

## Goal

An amendment to ADR-0103, decided by Claude under Stewart's delegation, says how a `tools/call` longer than the message limit is answered so the calling client gets an answer to its own request (matched by its `id`) instead of waiting for ever, and whether `surl-mcp`'s limit or its tool descriptions change.

## Context

Surly cookbook bug report 2026-11-20, item 2 (Surl `9ad60d80c`): `start_server` given about 5 MB of `files` (50 files of 100,000 characters) never returns; 50 files of 50,000 characters (about 2.5 MB) works. The report expected an answer or a refusal naming a size limit.

What happens, in today's code (`factory/phase-1` at `299737d24`) - no deadlock and no blocking read:
- `Application/Surl.Mcp/Program.cs:179` builds `McpSessionOptions` without a limit, so the default `McpSessionOptions.DefaultMaximumMessageBytes` = 4 MiB (`Protocols/Http/Surl.McpServing.UnitLibrary/McpSessionOptions.cs:7`) applies. 5,000,000 bytes of file text plus JSON is past 4,194,304.
- `McpInputLineReader.cs:81-93` discards the over-long line unbuffered and `EndLine` (line 100-106) returns an empty message; `McpStreamSession.AnswerAsync` writes `McpRequestDispatcher.AnswerMessageTooLong()` (`McpRequestDispatcher.cs:72-73`): `-32600 Invalid Request: the message is longer than 4194304 bytes` **with a null `id`**, and the session continues. `McpStreamSessionTests.RunAsync_LineOverTheLimit_AnswersInvalidRequestWithANullIdAndContinues` pins that.
- ADR-0103 decision 1, "The message limit", decided exactly this (null `id`, session continues). JSON-RPC 2.0 allows a null `id` only when the id could not be detected; an MCP client matches responses by `id`, so it never matches this one and its `tools/call` waits for ever. That is the "never returns".

What the amendment must settle:
1. Whether the refusal carries the request's own `id` when it can be found in the discarded line, and how, without buffering the line: e.g. a streaming scan that tracks JSON nesting and string escapes and keeps the top-level `"id"` value (a number or a string of at most a few hundred bytes) wherever it appears, since clients put it first or last. A line whose `id` cannot be read keeps today's null-`id` answer.
2. Whether `surl-mcp`'s limit stays 4 MiB, grows, or becomes a `surl-mcp` option; `start_server` keeps whole files in memory, and ADR-0103's other caps (64 KiB and 1 MiB per result, 200 MiB of recordings) bound memory already.
3. Whether `start_server`'s and `put_file`'s tool descriptions name the limit, so an agent can split its seeding (`put_file` per file) before it hits it.
4. That the extended MCP server (`--max-message`) gets the same answer, since it shares `Surl.McpServing`.

Stand-in: `surl-mcp` is how an agent seeds a stand-in; a seed too large must fail fast with a reason, never hang a test run.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md` has a dated amendment, marked "Decided by Claude under Stewart's delegation", to decision 1's "The message limit", answering points 1 to 4.
- [ ] The amendment states the exact refusal (code, message text, `id`) for an over-long line whose `id` can be read and for one whose `id` cannot.
- [ ] The amendment cites the JSON-RPC 2.0 specification's rule on the `id` of an error response and the MCP specification revision it reads (by URL and revision date).
- [ ] Every task the amendment needs beyond BL-1523 (a `surl-mcp` option, tool description text, `--aihelp` or README text) is filed, and BL-1523 depends on the ones it needs.
- [ ] `Documentation/Planning/Decisions/README.md`'s ADR-0103 row mentions the amendment.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-21: Backlog -> Doing.
