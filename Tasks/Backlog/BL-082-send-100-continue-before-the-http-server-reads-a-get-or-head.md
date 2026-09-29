---
id: BL-082
title: Send 100 Continue before the HTTP server reads a GET or HEAD body
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-050]
touches: [Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed:
---
# BL-082 — Send 100 Continue before the HTTP server reads a GET or HEAD body

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

- [ ] A fast test proves a `GET` with `Expect: 100-continue` and a body within the limit
      gets exactly `HTTP/1.1 100 Continue\r\n\r\n` before the 200, and that no `100
      Continue` is sent for HTTP/1.0 or without the expectation.
- [ ] The exchange was recorded with pinned upstream curl 8.21.0 and the recording is
      committed under `Surl.Protocol.Http.UnitTests/Fixtures/`.
- [ ] `dotnet build` is clean, the fast tests are green, and `Measure-CodeQuality.ps1`
      reports no failing member in `Surl.Protocol.Http.UnitLibrary`.

## Notes

Filed by BL-050 (2026-09-29).

## Log

- 2026-09-29: Created.
