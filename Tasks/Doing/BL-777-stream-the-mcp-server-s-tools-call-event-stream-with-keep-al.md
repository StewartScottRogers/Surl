---
id: BL-777
title: Stream the MCP server's tools/call event stream with keep-alive comments
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-466, BL-1035]
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

- [x] A `Surl.HttpServing.UnitTests` test shows a streamed body written chunked, with the bytes from RFC 9112 section 7.1.
- [x] A `Surl.Extended.Protocol.McpServer.UnitTests` test shows a `tools/call` running past 15 seconds on a fake clock writes `: keep-alive\n\n` before its `data:` event.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 5): code written and left uncommitted for the shift to stash. `HttpAnswerBody.Streamed`
  takes `Func<HttpAnswerChunkWriter, CancellationToken, Task>`; `Http1AnswerWriter` adds
  `Transfer-Encoding: chunked` after the answer's fields, writes each non-empty piece as one chunk (lowercase hex
  size) and `0\r\n\r\n`; over HTTP/1.0 (no chunked coding) the body goes unframed and the connection closes
  (RFC 9112 section 6.3). Choice: the writer, not the handler, adds `Transfer-Encoding`, since framing is the
  version writer's job as `Connection` already is. HTTP/2 and HTTP/3 writers in `Surl.Protocol.Http` are not
  taught `Streamed`: only the HTTP server's own handler feeds them and it never makes one.
- `McpEventStream.WriteAsync` races the call against `Task.Delay(15 s, TimeProvider)` and writes
  `: keep-alive\n\n` each time the delay wins; a cancelled call (null) writes nothing more. The fault check and
  the call now run inside the stream callback (`McpHttpRequestAnswerer.AnswerRequestAsync`).
- Tests: `Http1AnswerWriterTests.WriteAsync_StreamedBodyOverHttp11_WritesEachPieceAsAChunkThenTheLastChunk`
  (the 4/13 chunk example), `..._StreamedBodyOverHttp10_...`; `McpEventStreamTests.WriteAsync_ACallRunningPast15Seconds_WritesAKeepAliveCommentBeforeItsDataEvent`
  and two more on a `ManualTimeProvider` copied into the test project; the three tools/call server tests now
  expect the chunked event (`McpHttpHarness.EventStreamAnswer`).
- Verified only with the installed SDK 11.0.100 run from outside the repository (global.json not applied):
  `Surl.HttpServing.UnitTests` 40/40, `Surl.Extended.Protocol.McpServer.UnitTests` 297/297, `-warnaserror` clean.
  The pinned SDK 10.0.401 is not installed, so the third criterion (pinned build, coverage) waits on BL-1035.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so the pinned build, fast tests and coverage cannot run; code is written and stashed by the shift
- 2026-11-11: Backlog -> Doing.
