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
completed:
---
# BL-1776 — Reset an HTTP/3 request whose pseudo-header value holds NUL, CR or LF

## Goal

An HTTP/3 request whose pseudo-header value holds NUL, CR or LF is reset H3_MESSAGE_ERROR and never reaches a handler.

## Context

- Found by the code review of BL-1773 (the HTTP/3 one), read from the code, not reproduced.
- Http3RequestHeadMapping.AddPseudoHeader checks order, name and repeats, never the value, while the class remarks and
  RFC 9114 section 10.3 make such a value malformed. A regular field's value is already checked.

## Acceptance criteria

- [ ] Http3RequestHeadMappingTests show NUL, CR and LF in :method, :scheme, :authority and :path each give a malformation.
- [ ] dotnet build -warnaserror and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
