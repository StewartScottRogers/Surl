---
id: BL-1776
title: Reset an HTTP/3 request whose pseudo-header value holds NUL, CR or LF
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-26
---
# BL-1776 — Reset an HTTP/3 request whose pseudo-header value holds NUL, CR or LF

## Goal

An HTTP/3 request whose pseudo-header value holds NUL, CR or LF is reset H3_MESSAGE_ERROR and never reaches a handler.

## Context

- Found by the code review of BL-1773 (the HTTP/3 one), read from the code, not reproduced.
- Http3RequestHeadMapping.AddPseudoHeader checks order, name and repeats, never the value, while the class remarks and
  RFC 9114 section 10.3 make such a value malformed. A regular field's value is already checked.

## Acceptance criteria

- [x] Http3RequestHeadMappingTests show NUL, CR and LF in :method, :scheme, :authority and :path each give a malformation.
- [x] dotnet build -warnaserror and the fast tests are green.

## Notes

- Reproduced first: 13 new test cases failed (a head was returned), then passed with the fix.
- `Http3RequestHeadMapping.AddPseudoHeader` now applies the same NUL, CR and LF check as a regular field's value.
  The existing `Http3RequestStreamServer` path resets any malformation `H3_MESSAGE_ERROR` before the handler is called.
- Choice: the check covers every pseudo-header a request may carry, `:protocol` included, because RFC 9114 section 10.3
  makes no exception for it.
- Choice: no leading or trailing space or tab check on pseudo-header values; the goal names NUL, CR and LF only.
  No new ADR: ADR-0082 decision 7 already says a malformed request is reset.
- No upstream curl measurement: the change adds no byte Surl sends beyond the existing reset.
- HTTP/2 has the same gap, already filed as BL-1777.
- Verify: build 0 warnings, format clean, fast tests 29804 passed, 0 failed, 3 skipped.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-26: Doing -> Done. An HTTP/3 request whose pseudo-header value holds NUL, CR or LF is reset H3_MESSAGE_ERROR and never reaches a handler.
