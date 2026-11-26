---
id: BL-1777
title: Reset an HTTP/2 request whose pseudo-header value holds NUL, CR or LF
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-26
---
# BL-1777 — Reset an HTTP/2 request whose pseudo-header value holds NUL, CR or LF

## Goal

An HTTP/2 request whose pseudo-header value holds NUL, CR or LF is refused as malformed and never reaches a handler.

## Context

- Found while delivering BL-1773 (the HTTP/3 one), read from the code, not reproduced.
- Http2RequestHeadMapping.AddPseudoHeader checks order, name and repeats, never the value; RFC 9113 section 8.2.1 makes
  such a value malformed. The HTTP/3 twin is BL-1776.

## Acceptance criteria

- [x] Http2RequestHeadMappingTests show NUL, CR and LF in each pseudo-header field give a malformation.
- [x] dotnet build -warnaserror and the fast tests are green.

## Notes

- 2026-11-26: Http2RequestHeadMapping.AddPseudoHeader now checks the value; Map_PseudoHeaderValueHoldingNulCrOrLf_IsMalformed
  (12 cases: :method, :scheme, :authority, :path) failed before the fix and passes after. :protocol is already refused in
  HTTP/2 as unknown.
- 2026-11-26: Same rule and message as the HTTP/3 twin (BL-1776): only NUL, CR and LF; surrounding whitespace in a
  pseudo-header value is not newly checked. No ADR: ADR-0081 decision 5 already resets a malformed request with
  RST_STREAM PROTOCOL_ERROR. No upstream curl measurement: no new byte is sent.
- 2026-11-26: verify: build 0 warnings, format clean, fast tests 29816 passed, 3 skipped, 0 failed (83 assemblies).

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. An HTTP/2 request whose pseudo-header value holds NUL, CR or LF is refused as malformed and never reaches a handler.
