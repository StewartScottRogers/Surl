---
id: BL-1739
title: Remove ADR-0182 Amendment 8's false limit about a preface after a Content-Length: 0 head
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1739 — Remove ADR-0182 Amendment 8's false limit about a preface after a Content-Length: 0 head

## Goal

ADR-0182 no longer says that an upgrade request declaring `Content-Length: 0` makes the proxy withhold the HTTP/2
preface that follows it.

## Context

- Amendment 8's "Accepted limits" in
  `Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md`
  holds the bullet ending `(BL-1736)`. It is false: `HttpRequestBodyFraming.Of` reads `Content-Length: 0` as no body,
  so `HttpRequestHeadBytesRedactor.IsAtRequestStart` is true right after such a head, and was when the bullet was
  written.
- Proof: `HttpRequestHeadBytesRedactorTests.IsAtRequestStart_RightAfterAHeadDeclaringContentLengthZero_IsTrue`
  (added by BL-1736, which changed no production code).

## Acceptance criteria

- [ ] ADR-0182 holds no statement that `HttpRequestHeadBytesRedactor` is not at a request start after a
      `Content-Length: 0` head; the bullet citing BL-1736 under Amendment 8's accepted limits is removed.
- [ ] No other line of ADR-0182 changes.

## Notes

## Log

- 2026-11-25: Created.
