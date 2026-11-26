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
completed:
---
# BL-1777 — Reset an HTTP/2 request whose pseudo-header value holds NUL, CR or LF

## Goal

An HTTP/2 request whose pseudo-header value holds NUL, CR or LF is refused as malformed and never reaches a handler.

## Context

- Found while delivering BL-1773 (the HTTP/3 one), read from the code, not reproduced.
- Http2RequestHeadMapping.AddPseudoHeader checks order, name and repeats, never the value; RFC 9113 section 8.2.1 makes
  such a value malformed. The HTTP/3 twin is BL-1776.

## Acceptance criteria

- [ ] Http2RequestHeadMappingTests show NUL, CR and LF in each pseudo-header field give a malformation.
- [ ] dotnet build -warnaserror and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing.
