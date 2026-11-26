---
id: BL-1781
title: Redact a WebSocket upgrade head that starts with an empty line
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Ws.UnitLibrary, Protocols/Http/Surl.Protocol.Ws.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-1781 — Redact a WebSocket upgrade head that starts with an empty line

## Goal

The `ws` server's exchange log withholds the upgrade request's credentials when empty lines come before the request
line.

## Context

- Found by the review of BL-1771, read from the code and not reproduced.
- `WebSocketUpgradeHeadBytesRedactor.LengthThroughHeadEnd` starts with `lineIsEmpty` set, so a leading `\r\n` or `\n`
  is taken for the end of the head, and every later byte, `Authorization` included, passes through unredacted.
- `HttpConnectionReader` skips empty lines before the request line (RFC 9112, section 2.2) and accepts the request.
- `HttpRequestHeadBytesRedactor` already handles such lines; only the WebSocket redactor's own head-end search needs to
  ignore empty lines until a non-empty one has been seen.
- Only the levelled log and trace dumps change; the event stream keeps raw bytes (ADR-0182 decision 4).

## Acceptance criteria

- [ ] A test in `Surl.Protocol.Ws.UnitTests` passes in which an upgrade head after a leading `\r\n`, and after a leading
      `\n`, is logged with `Authorization: Basic ****`, also one byte at a time.
- [ ] Frames after that head still pass through unchanged, pinned by the same or another test.
- [ ] A new ADR-0182 amendment states the behaviour.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-26: Created.
