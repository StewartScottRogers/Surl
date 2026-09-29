---
id: BL-061
title: Drain unread request bytes before the HTTP server closes, and refuse an invalid Content-Length
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-018]
touches: [Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-061 — Drain unread request bytes before the HTTP server closes, and refuse an invalid Content-Length

## Goal

When `HttpProtocolServer` closes a connection after a response while request bytes are
still unread (a refused `POST` body, a GET that announced a body, the rest of a malformed
head), it drains them for a bounded time and byte count before the close, so the peer's
kernel does not answer the unread bytes with a TCP reset that destroys the response. A
request whose `Content-Length` is invalid is answered `400 Bad Request` and closed.

## Context

- BL-018 built the server (`HttpRequestResponder`, ADR-0008). Every refusal half-closes
  with `CompleteWritesAsync` and returns; the engine then disposes the connection with
  bytes still unread, which on Windows and Linux sends RST (RFC 9112, section 9.6,
  "lingering close").
- RFC 9112, section 6.3, item 5: an invalid `Content-Length` (not digits, several values
  that differ) is unrecoverable; the server answers 400 and closes. Today such a request
  is served and closed.
- Read through `HttpConnectionReader.ReadAsync`, which returns buffered bytes first. Bound
  the drain on the exchange context's `TimeProvider` (ADR-0006, section 5 uses a one
  second write deadline for refusals) and a byte count; decide both and record them in a
  new ADR that supersedes the affected ADR-0008 rows.
- Measure with `Record-CurlExchange.ps1` first: what pinned upstream curl 8.21.0 does with
  `curl -sS --fail -d x http://127.0.0.1:<port>/file.txt` when answered 405 with
  `Connection: close`.

## Acceptance criteria

- [ ] A fast test proves a refused `POST` with a body has the body read before the
      connection is disposed, and that the drain stops at the decided byte count and at
      the decided time on a fake `TimeProvider`.
- [ ] Fast tests cover `Content-Length: abc`, `Content-Length: -1` and two differing
      `Content-Length` fields, each answered 400 with `Connection: close`.
- [ ] The 405 answer to `curl --fail -d x` was fed to pinned upstream curl 8.21.0 with
      `Record-CurlExchange.ps1 -Response`, and the committed recording shows exit code 22.
- [ ] `dotnet build` is clean, the fast tests are green, and `Measure-CodeQuality.ps1`
      reports no failing member in `Surl.Protocol.Http.UnitLibrary`.

## Notes

Filed by BL-018 from its code review (2026-09-28).

BL-050 (2026-09-29, ADR-0019): a `GET` or `HEAD` whose body has a readable framing (one
valid `Content-Length`, or `Transfer-Encoding: chunked` alone) now has its body read and
discarded, and keeps the connection. What is left for this task: refused methods, which
still never read their body, and a body with a framing `HttpRequestBodyFraming` calls
`Unreadable` (served and closed unread today), which includes the invalid
`Content-Length` this task answers 400.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
