---
id: BL-1660
title: Have the HTTP server set the configured cookies and record the Cookie headers through Surl.Cookies
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1659]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: FR-015
created: 2026-11-24
completed:
---
# BL-1660 — Have the HTTP server set the configured cookies and record the Cookie headers through Surl.Cookies

## Goal

With cookies configured as BL-1650's ADR decides, the HTTP server adds their `Set-Cookie`
fields to the replies the ADR names and publishes each request's `Cookie` pairs on the
exchange event stream; with none configured, every reply is byte for byte unchanged.

## Context

Depends on the library from BL-1659. The server reaches `Surl.Cookies` only through the
contract the ADR names (a horizontal library, ADR-0002 decision 3). No option parsing
here; that is BL-1661.

## Acceptance criteria

- [ ] A named test in `Surl.Protocol.Http.UnitTests` shows a configured cookie's
      `Set-Cookie` field on a `GET` reply, with the bytes the ADR pins.
- [ ] A named test shows a request's `Cookie` header published on the event stream as the
      ADR's pairs.
- [ ] A named test shows that with no cookie configured the reply carries no `Set-Cookie`
      and is unchanged from today.
- [ ] 100% line and branch coverage; build clean and fast tests green.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
