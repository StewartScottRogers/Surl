---
id: BL-720
title: Let an S3 head past --max-request-head be refused 400 RequestHeaderSectionTooLarge
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-418]
touches: [Protocols/Http/Surl.HttpServing.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-720 — Let an S3 head past --max-request-head be refused 400 RequestHeaderSectionTooLarge

## Goal

A request head past `--max-request-head` on the `s3` server is answered `400 Bad Request` with the S3 XML
`Error` body `<Code>RequestHeaderSectionTooLarge</Code>`, then closed, as ADR-0108 decision 7 says, while the
HTTP server's `431` stays byte for byte as it is.

## Context

- ADR-0108 decision 1.1's table and decision 7; matrix row `S3:Surl-Limits-1` in
  `Documentation/Conformance/Extended/S3.md`.
- Today `Surl.HttpServing`'s `Http1RequestResponder.AnswerHeadNotReadAsync` builds every malformed-head refusal
  itself (`431` for `HeadTooLarge`), before any `IHttpRequestHandler` sees it, so BL-418's S3 server answers
  `431` with an empty body (pinned by `S3ProtocolServerTests.ServeAsync_HeadPastMaxRequestHead_IsRefused431AndClosedAsAdr0006Says`).
- Needs a seam in `Surl.HttpServing` letting a handler word a head-not-read refusal (for example an optional
  interface the handler implements, given the outcome and returning an `HttpAnswer`), defaulting to today's
  answers so the HTTP server's recorded fixtures do not change.
- The S3 message, from the "List of error codes" table: `Your request header section exceeds the maximum allowed size.`
  `Resource` is empty: no request target was read.

## Acceptance criteria

- [ ] `Surl.HttpServing` has the seam, with tests in `Surl.HttpServing.UnitTests`; every existing HTTP test passes unchanged.
- [ ] A named S3 test shows a head past the limit answered `400` with `RequestHeaderSectionTooLarge`, `x-amz-request-id`
      matching the body's `RequestId`, and `Connection: close`; the BL-418 `431` test is replaced by it.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

- Filed by BL-418 (2026-11-05), which kept the `431` because `Surl.HttpServing` was outside its `touches`.

## Log

- 2026-11-05: Created.
