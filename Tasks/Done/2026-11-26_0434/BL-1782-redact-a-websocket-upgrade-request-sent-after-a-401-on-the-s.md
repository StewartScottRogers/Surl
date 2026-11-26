---
id: BL-1782
title: Redact a WebSocket upgrade request sent after a 401 on the same connection
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Ws.UnitLibrary, Protocols/Http/Surl.Protocol.Ws.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1782 — Redact a WebSocket upgrade request sent after a 401 on the same connection

## Goal

The `ws` server's exchange log withholds the credentials of every upgrade request on a connection, not only the first.

## Context

- Found while doing BL-1781, read from the code and not reproduced.
- A `401` keeps the connection and its next request is read as a new upgrade request
  (`WebSocketUpgradeResponder.ChallengeAsync`, ADR-0032), but `WebSocketUpgradeHeadBytesRedactor` stops redacting at
  the end of the first head. So the `Authorization` of the retried request, as `curl --anyauth` or `--digest` sends it,
  is logged as received.
- The redactor sees what the server sends through `ObserveSent`, so a `101` can tell it where frames begin. A client's
  bytes after the first head are redacted before the server has answered, and the choice must fail closed.
- The same stop has a second gap (ADR-0182 amendment 26): a line of carriage returns alone after the request line
  ends the redactor's search though the server refuses the head, so a field after it is logged as received.
- Measure what pinned upstream curl sends on such a connection with `Record-CurlExchange.ps1`.
- Only the levelled log and trace dumps change; the event stream keeps raw bytes (ADR-0182 decision 4).

## Acceptance criteria

- [x] A test in `Surl.Protocol.Ws.UnitTests` passes in which the second upgrade head on one connection, after the first
      was answered `401`, is logged with `Authorization: Basic ****`, also one byte at a time.
- [x] Frames after the head answered `101` still pass through unchanged, pinned by the same or another test.
- [x] A new ADR-0182 amendment states the behaviour and removes amendment 26's accepted limit.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch coverage.

## Notes

- Fix: `WebSocketUpgradeHeadBytesRedactor` gives every byte to `HttpRequestHeadBytesRedactor` until it sees the server
  send `HTTP/1.1 101 `, then flushes any held line and passes bytes through. Both gaps close (ADR-0182 amendment 27).
- Choice: bytes read before the server answers are judged as a head, frames sent that early included (fail closed). The
  old test pinning those as unchanged now expects them masked. Rejected: restarting the mask on each `401`, since a
  client may send its next request before the `401` is observed.
- Measured with pinned curl 8.21.0: on a `ws` `401`, `--anyauth` and `--digest` close the connection (exit 22) and send
  no second request (fixture `anyauth-401-101`). So the Context's "as curl --anyauth or --digest sends it" is not what
  curl does on `ws`; the second head comes from another client.
- Accepted limit (in amendment 27): bytes a head declares as its body are logged as received, as amendment 24 has it.
- Verified: 6 new tests failed on the old redactor; build `-warnaserror` 0 warnings; format clean; fast tests green in
  83 projects (Ws 136); `Surl.Protocol.Ws.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The ws exchange log withholds the Authorization of every upgrade request until the server sends 101 (ADR-0182 amendment 27)
