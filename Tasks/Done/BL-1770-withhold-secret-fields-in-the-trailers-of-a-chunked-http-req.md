---
id: BL-1770
title: Withhold secret fields in the trailers of a chunked HTTP request body from the exchange log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1759]
touches: [Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary]
requirement: none
created: 2026-11-25
completed: 2026-11-26
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

- [x] `HttpRequestHeadBytesRedactorTests.Redact_SecretFieldInChunkedTrailers_IsMasked` passes for `Authorization` and
      a further field name, also one byte at a time.
- [x] A request following such a body on the same connection is still redacted, pinned by a test.
- [x] A new ADR-0182 amendment and the redactor's doc comment state the behaviour and which limit it closes.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch coverage.

## Notes

- Plan: a chunked body's trailer section goes through the same line redactor and `HttpRequestHeadLineRule` as a head;
  `StartTrailerSection` makes the first line a field, not a request line. ADR-0182 amendment 32 records it.
- Made directly in the session, without `protocol-architect` or `test-writer`: the change is one redactor and its line
  rule. Tests were written first and failed (6) before the code. `code-reviewer` reviewed the diff: no new leak; its
  wording findings and the `\r\r\n` test row are in.
- No conformance run: only the log changes, no wire byte, and curl is not known to send request trailers from the
  command line.
- Behaviour change: a trailer line of carriage returns alone no longer ends the section, as the servers read it.
- `touches` widened (no other task was in `Doing` in this checkout): `Application/Surl.Cli.UnitLibrary` and its tests,
  because `--aihelp logging` and the manual said trailers are logged as received (root `CLAUDE.md` has them change in
  the same diff); `Surl.Protocol.HttpProxy.UnitLibrary`, for one doc comment that named the trailer limit.
- Measured: `Measure-CodeQuality.ps1 -Library Surl.HttpMessage.UnitLibrary` gives 100% line, 100% branch, 0 failing.

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. A secret field in a chunked HTTP/1.x request body's trailers is withheld from the exchange log and trace dumps (ADR-0182 amendment 32)
