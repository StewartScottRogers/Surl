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
completed: 2026-11-26
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

- [x] `HttpRequestHeadBytesRedactorTests.Redact_HeadLinesEndedByBareLineFeeds_MasksEachSecretField` passes for the
      input above and for an `Authorization` line, also one byte at a time.
- [x] A second request on the same connection, after a bare-line-feed head with a body, is redacted, pinned by a test.
- [x] A new ADR-0182 amendment and the redactor's doc comment state the behaviour and which limit it closes.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch coverage.

## Notes

- Done in the session without the planning, test-writer and implementer agents: one class and its rule. Tests were
  written first and failed (9 of 35) before the change. `code-reviewer` ran; no conformance stage, because no byte Surl
  sends or accepts changes.
- `HttpRequestHeadBytesRedactor` hands `LineBytesRedactor` a CR LF for a bare line feed and drops the CR it writes
  back, so the shared CR LF line redactor in `Surl.Protocol.Abstractions` is untouched.
- Review found two lines the line-by-line judging newly logged, both fixed to fail closed (amendment 24, points 4 and 5):
  a colon-less line after a secret field, and a secret field right after a request line longer than 64 KiB.
- `HttpConnectionReaderTests` gained one test: `HttpConnectionReader`'s too-large check on an already buffered line was
  an uncovered branch, and the library has to be at 100%.
- `--aihelp logging` and the manual still name the closed limit. BL-1764 owns that text and is not finished, so BL-1771
  was added to its `depends-on`; no help-text follow-up is needed.
- Filed BL-1781 from the review: the `ws` redactor takes a leading empty line for the end of the head.

Evidence: dotnet build -warnaserror clean; 29880 fast tests passed in 83 assemblies;
`Surl.HttpMessage.UnitLibrary` at 100% line and branch coverage.

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The exchange log withholds secret fields in HTTP and RTSP request heads whose lines end with a bare line feed (ADR-0182 amendment 24)
