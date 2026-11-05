---
id: BL-621
title: Serve an MCP session over newline-delimited stdio streams in Surl.McpServing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-620]
touches: [Protocols/Http/Surl.McpServing.UnitLibrary, Protocols/Http/Surl.McpServing.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-621 — Serve an MCP session over newline-delimited stdio streams in Surl.McpServing

## Goal

`Surl.McpServing` serves one MCP session over a pair of byte streams given by its caller: it reads
newline-delimited JSON-RPC messages from one, writes only MCP messages, each on one line, to the other, answers
requests concurrently as the ADR allows, honours `notifications/cancelled`, and ends cleanly when its input closes.
Both the `Surl.Mcp` host and the extended MCP server's `mcp+stdio` bind it to real standard streams.

## Context

- Decision: BL-612's ADR, decisions 1 and 5 (the stream-pair seam's shape, the line limit, concurrency, and what
  ending the input does). This takes over the generic half of BL-467.
- MCP "Transports > stdio" (https://modelcontextprotocol.io/specification/2025-06-18/basic/transports): messages are
  delimited by newlines and must not contain embedded newlines; the server must not write anything to its output
  that is not a valid MCP message, and may log to standard error. "Lifecycle > Shutdown": the client closes the
  server's input. "Utilities > Cancellation": `notifications/cancelled` with the `requestId`.
- The library never touches `Console`; it takes `Stream`s (or the pipe types the ADR names). Tests use in-memory
  streams, `.claude/rules/testing.md`.

## Acceptance criteria

- [x] Named tests over in-memory streams show: an `initialize`, `tools/list` and `tools/call` exchange with each
      response on exactly one line; a JSON string containing `\n` written escaped; nothing but MCP messages written
      to the output; the session returning when the input closes; a line over the limit refused as the ADR says;
      a `notifications/cancelled` for a running call cancelling that handler's token.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Delivered as ADR-0103 decision 1 states: `McpStreamSession.RunAsync(Stream, Stream, McpServerRegistry,
  McpSessionOptions, CancellationToken)` plus the internal `McpInputLineReader`. No new ADR: every choice
  below fills a detail the ADR leaves open without departing from it.
- Choices taken (sensible defaults):
  - An unterminated last line (input ends without `\n`) is answered like any other message, as a lenient
    reader does; dropping it would silently lose a client's last request.
  - The cancellation key is the id's kind plus its value: a string id and a number id never match each
    other (`"5"` is not `5`), and a number matches by its JSON text. A client that reuses a running
    request's id can cancel only the first of the two.
  - Back-pressure is literal to the ADR: a slot is taken *before* each message is read, so with 16 calls
    running the 17th message stays unread (a `notifications/cancelled` behind it waits too, as the ADR
    accepts).
  - Messages answered synchronously (everything but a running `tools/call`) are written before the next
    line is read, so `initialize` is always answered before a request pipelined behind it.
  - A cancelled request's response is never written, even when its handler ignores the token and returns
    (MCP "Cancellation": SHOULD NOT respond).
  - A failed read is thrown after the running handlers end; a failed write of a running answer is kept and
    thrown once the session ends. Once the caller's token has ended the session, how the read ended no
    longer matters and `RunAsync` returns normally.
- Tests: `McpStreamSessionTests` (19 cases) and `McpInputLineReaderTests` (6) over in-memory streams
  (`UnitTestChannelInputStream`, `UnitTestLineOutputStream`); the library is at 100% line and branch
  coverage; the back-pressure test ran 25 times clean after removing a race in the test itself.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. McpStreamSession serves an MCP session over newline-delimited stream pairs with 16 concurrent requests, back-pressure and notifications/cancelled
