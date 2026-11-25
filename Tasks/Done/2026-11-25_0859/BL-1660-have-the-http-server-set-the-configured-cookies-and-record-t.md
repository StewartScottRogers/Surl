---
id: BL-1660
title: Have the HTTP server set the configured cookies and record the Cookie headers through Surl.Cookies
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1659]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Cookies.UnitLibrary, Application/Surl.Api, Application/Surl.Api.UnitTests, Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: FR-015
created: 2026-11-24
completed: 2026-11-24
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

- [x] A named test in `Surl.Protocol.Http.UnitTests` shows a configured cookie's
      `Set-Cookie` field on a `GET` reply, with the bytes the ADR pins.
- [x] A named test shows a request's `Cookie` header published on the event stream as the
      ADR's pairs.
- [x] A named test shows that with no cookie configured the reply carries no `Set-Cookie`
      and is unchanged from today.
- [x] 100% line and branch coverage; build clean and fast tests green.

## Notes

- Added to `touches` (no task in Doing named them): `Surl.Protocol.Abstractions` (the new `CookieReceivedEvent`, ADR-0189 decision 5, and the isolation test row for `Surl.Cookies`), `Surl.Cookies`, and the `Surl.Api`, `Surl.Control` and `Surl.Mcp` event lists, which the ADR-0171 guard tests fail on without a kind, a sample and (control JSON) a `value` member.
- `SurlEventQuery.Name` now also matches a cookie event by exact name.
- `CookieHandler` wraps the HTTP/1.x and HTTP/2 answerers; `Http3ProtocolServer` is not wired (HTTP/3 is not in the acceptance criteria) - left for a follow-up if BL-1662 needs it.
- Value bound: 256 UTF-8 bytes, never ending in half a surrogate pair.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. HTTP server sets configured Set-Cookie fields and publishes CookieReceivedEvent per ADR-0189; tests green
