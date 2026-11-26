---
id: BL-1770
title: Withhold secret fields in the trailers of a chunked HTTP request body from the exchange log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1759]
touches: [Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1770 — Withhold secret fields in the trailers of a chunked HTTP request body from the exchange log

## Goal

`HttpRequestHeadBytesRedactor` withholds the value of a secret field sent in the trailers of a chunked request body.

## Context

- Found by the review of BL-1759 (ADR-0182 amendment 18, third accepted limit).
- The redactor passes trailer bytes through unchanged (`ReadTrailerByte`), so
  `...Transfer-Encoding: chunked\r\n\r\n0\r\nX-Api-Key: s3cret\r\n\r\n` logs `s3cret`. HTTP/2 trailer blocks are
  already withheld.
- The secret fields are `HttpWithheldHeaderNames.Credentials` and whatever further names the redactor is given, as in
  the head. Folded continuation lines of such a field are masked too.
- Only the levelled log and trace dumps change; the event stream keeps raw bytes (ADR-0182 decision 4).

## Acceptance criteria

- [ ] `HttpRequestHeadBytesRedactorTests.Redact_SecretFieldInChunkedTrailers_IsMasked` passes for `Authorization` and
      a further field name, also one byte at a time.
- [ ] A request following such a body on the same connection is still redacted, pinned by a test.
- [ ] A new ADR-0182 amendment and the redactor's doc comment state the behaviour and which limit it closes.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing.
