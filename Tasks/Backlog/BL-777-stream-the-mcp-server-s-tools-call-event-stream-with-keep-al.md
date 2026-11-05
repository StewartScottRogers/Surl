---
id: BL-777
title: Stream the MCP server's tools/call event stream with keep-alive comments
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-466]
touches: [Protocols/Http/Surl.HttpServing.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitTests, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-777 — Stream the MCP server's tools/call event stream with keep-alive comments

## Goal

A `tools/call` on Streamable HTTP is answered with a chunked `text/event-stream` whose head is written at once,
a `: keep-alive` comment and blank line every 15 seconds on the injected `TimeProvider` while the tool runs, then
the one `data:` event, as ADR-0111 decision 4.3 says.

## Context

- BL-466 answers `tools/call` with the event as one `Content-Length` body (`McpEventStream`,
  `McpHttpRequestAnswerer.AnswerInSessionAsync`), because `Surl.HttpServing`'s `HttpAnswerBody` has only
  none, bytes and a content-store file: there is no body written while it is produced.
- Add a streamed body to `Surl.HttpServing` (an `HttpAnswerBody` case that writes to the connection
  through a callback, framed chunked by `Http1AnswerWriter`, RFC 9112 section 7.1), then use it here.
- SSE comment lines: WHATWG HTML "Server-sent events" (a line starting with `:` is ignored by the client).
- A cancelled call ends the stream with no event (decision 4.3).

## Acceptance criteria

- [ ] A `Surl.HttpServing.UnitTests` test shows a streamed body written chunked, with the bytes from RFC 9112 section 7.1.
- [ ] A `Surl.Extended.Protocol.McpServer.UnitTests` test shows a `tools/call` running past 15 seconds on a fake clock writes `: keep-alive\n\n` before its `data:` event.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
