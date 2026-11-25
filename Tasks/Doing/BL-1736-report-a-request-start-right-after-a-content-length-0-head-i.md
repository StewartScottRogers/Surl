---
id: BL-1736
title: Report a request start right after a Content-Length: 0 head in HttpRequestHeadBytesRedactor
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1736 — Report a request start right after a Content-Length: 0 head in HttpRequestHeadBytesRedactor

## Goal

`HttpRequestHeadBytesRedactor.IsAtRequestStart` is true immediately after a head that declares `Content-Length: 0`.

## Context

- Found while working BL-1713. In `Protocols/Http/Surl.HttpMessage.UnitLibrary/HttpRequestHeadBytesRedactor.cs`,
  `StartBody` enters the `Content-Length` body state with nothing remaining and leaves it only on the next byte, so the
  property is false between the head and that byte.
- The HTTP proxy's `HttpProxyConnectionBytesRedactor` asks the property before it hands bytes on, so it takes an `h2c`
  preface that follows such a head as misplaced and withholds every later byte.
- `RtspRequestBytesRedactor` asks it too, before reading `$` as an interleaved frame (found by reading the code; no
  test reproduces it). The fix covers both without changing either project.
- Tests: `Protocols/Http/Surl.HttpMessage.UnitTests/HttpRequestHeadBytesRedactorTests.cs`. A bug fix starts with the
  failing test.
- Stand-in: this changes only where the redactor sees a request boundary; no store, event or fault is involved.

## Acceptance criteria

- [ ] `HttpRequestHeadBytesRedactorTests.IsAtRequestStart_RightAfterAHeadDeclaringContentLengthZero_IsTrue` passes: the
      property is true after exactly the head's bytes, with no further byte given.
- [ ] `HttpRequestHeadBytesRedactorTests.Redact_NextHeadAfterAContentLengthZeroHead_MasksItsAuthorization` passes.
- [ ] The tests `HttpRequestHeadBytesRedactorTests` held before this task pass unchanged.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and `Surl.HttpMessage.UnitLibrary` stays at 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
