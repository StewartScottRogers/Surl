---
id: BL-083
title: Send 100 Continue before the HTTP server reads a GET or HEAD body
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-050]
touches: [Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-083 — Send 100 Continue before the HTTP server reads a GET or HEAD body

## Goal

When an HTTP/1.1 `GET` or `HEAD` carries `Expect: 100-continue` and a body within the
upload limit, `HttpProtocolServer` sends `HTTP/1.1 100 Continue` before it reads and
discards the body, so pinned upstream curl sends the body at once instead of after its
one-second `--expect100-timeout`.

## Context

- BL-050 (ADR-0019, decision 5) made the server read and discard a `GET` or `HEAD` body,
  and answer 413 in place of `100 Continue` when the declared length is past the limit;
  it never sends `100 Continue`. RFC 9110, section 10.1.1: a server that will read the
  content either answers with a final status at once or sends `100 Continue`.
- The body discard is `HttpRequestBodyDiscarder`, called from
  `HttpRequestResponder.AnswerFromContentStoreAsync`.
- Measure first (ADR-0003): feed `HTTP/1.1 100 Continue\r\n\r\n` followed by the 200 to
  pinned upstream curl 8.21.0 with `Record-CurlExchange.ps1`, curl run with
  `-X GET -H "Expect: 100-continue" --data-binary @<file>`, and record how long curl
  waits and what it sends.

## Acceptance criteria

- [x] A fast test proves a `GET` with `Expect: 100-continue` and a body within the limit
      gets exactly `HTTP/1.1 100 Continue\r\n\r\n` before the 200, and that no `100
      Continue` is sent for HTTP/1.0 or without the expectation.
- [x] The exchange was recorded with pinned upstream curl 8.21.0 and the recording is
      committed under `Surl.Protocol.Http.UnitTests/Fixtures/`.
- [x] `dotnet build` is clean, the fast tests are green, and `Measure-CodeQuality.ps1`
      reports no failing member in `Surl.Protocol.Http.UnitLibrary`.

## Notes

Filed by BL-050 (2026-09-29).

- `Record-CurlExchange.ps1` added to `touches`: measuring a `100 Continue` needed a new
  `-InterimResponse` parameter (sent once the head arrives, before the body is waited
  for), and no task in `Doing` named the script. While there, its `Content-Length` regex
  ended `[ \t]*$` under `(?m)`, which never matches before the CR of a CRLF line, so the
  recorder never waited for a body that arrived after the head; it now allows `\r?`.
- Measured (pinned curl 8.21.0, win-x64): with `100 Continue` curl sent its 16-byte body
  at once and exited 0 after 105 ms; without, it sent the head alone and took 1037 ms (its
  one-second `--expect100-timeout`). Recorded as `Fixtures/expect-continue-get`, whose
  `response.bin` is the 100 followed by `get-file`'s 200.
- Decisions in ADR-0027: the 100 goes out only for HTTP/1.1, only when `Expect` lists
  `100-continue` (any case, comma list), and only when a body will be read (not for no
  body, `Content-Length: 0`, or an unframable body); no 417 for other expectations; the
  interim write uses the exchange's cancellation, not the refusal deadline. ADR-0019's
  consequence bullet now points at ADR-0027.
- Tests: `ExpectContinueTests` (12 cases). Http tests 320 green; fast suite green;
  `Measure-CodeQuality.ps1 -Library Surl.Protocol.Http.UnitLibrary` reports 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The HTTP server sends HTTP/1.1 100 Continue before it reads a GET or HEAD body under Expect: 100-continue, so pinned curl 8.21.0 sends the body at once (105 ms, not 1037 ms)
