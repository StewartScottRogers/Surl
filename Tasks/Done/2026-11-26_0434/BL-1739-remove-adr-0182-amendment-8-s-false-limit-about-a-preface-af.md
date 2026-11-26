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
completed: 2026-11-26
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

- [x] ADR-0182 holds no statement that `HttpRequestHeadBytesRedactor` is not at a request start after a
      `Content-Length: 0` head; the bullet citing BL-1736 under Amendment 8's accepted limits is removed.
- [x] No other line of ADR-0182 changes.

## Notes

- Removed the one two-line bullet; `git diff --numstat` showed 0 lines added and 2 removed, and the ADR holds no
  remaining `Content-Length: 0` or `BL-1736` text. `HttpRequestBodyFraming.OfContentLength` was read to confirm a zero
  length is no body.
- Edited in the session rather than through `align-and-document`: the whole change is one exact deletion the task
  already named, so a subagent would only have added tokens.
- Verify: build with `-warnaserror` clean, `dotnet format --verify-no-changes` clean, fast tests 29976 passed,
  0 failed, 3 skipped. No integration run: documentation only.

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. ADR-0182 Amendment 8 no longer claims a preface after a Content-Length: 0 head is withheld
