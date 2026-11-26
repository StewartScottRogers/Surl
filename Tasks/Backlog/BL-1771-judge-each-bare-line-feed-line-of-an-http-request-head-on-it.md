---
id: BL-1771
title: Judge each bare-line-feed line of an HTTP request head on its own in the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1759]
touches: [Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1771 — Judge each bare-line-feed line of an HTTP request head on its own in the exchange log

## Goal

`HttpRequestHeadBytesRedactor` withholds a secret field's value in a request head whose lines end with a bare line
feed.

## Context

- Found by the review of BL-1759 (ADR-0182 amendment 18, first accepted limit; amendment 16 had it for the credential
  fields).
- `HttpConnectionReader` accepts a bare line feed. The redactor's `LineBytesRedactor` ends a line at CR LF only, so
  `GET / HTTP/1.1\r\nHost: o\nX-Api-Key: s3cret\n\n` is judged as one `Host` line and logs `s3cret`. The same holds for
  `Authorization`, on the HTTP servers and on the HTTP proxy.
- Check what ends the head and where the body starts when lines end with a bare line feed: the redactor must still pass
  the body through and redact the next head.
- `--aihelp logging` (`AiHelpProse.cs`) and the manual name the proxy case as logged; BL-1764 owns that text. If this
  task finishes after BL-1764, file a help-text follow-up.
- Only the levelled log and trace dumps change; the event stream keeps raw bytes (ADR-0182 decision 4).

## Acceptance criteria

- [ ] `HttpRequestHeadBytesRedactorTests.Redact_HeadLinesEndedByBareLineFeeds_MasksEachSecretField` passes for the
      input above and for an `Authorization` line, also one byte at a time.
- [ ] A second request on the same connection, after a bare-line-feed head with a body, is redacted, pinned by a test.
- [ ] A new ADR-0182 amendment and the redactor's doc comment state the behaviour and which limit it closes.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-25: Created.
