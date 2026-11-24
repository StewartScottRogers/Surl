---
id: BL-1674
title: Set the configured cookies and publish received cookies in Http3ProtocolServer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: FR-015
created: 2026-11-24
completed:
---
# BL-1674 — Set the configured cookies and publish received cookies in Http3ProtocolServer

## Goal

`Http3ProtocolServer` adds the configured `Set-Cookie` fields to HTTP/3 replies and publishes a `CookieReceivedEvent` for each cookie a request carries, as `HttpProtocolServer` does.

## Context

ADR-0189 decision 2 says every reply on every HTTP or HTTPS listener. BL-1660 wired `CookieHandler` into `HttpProtocolServer` only (HTTP/1.x, HTTP/2, h2c). The library's `CLAUDE.md` Cookies paragraph also still says `HttpProtocolServer` has no option reader (BL-1661); BL-1661 and BL-1673 are done, so correct it.

## Acceptance criteria

- [ ] A named test in `Surl.Protocol.Http.UnitTests` shows a configured cookie's `set-cookie` field on an HTTP/3 reply.
- [ ] A named test shows an HTTP/3 request's `cookie` field published as `CookieReceivedEvent`s.
- [ ] A named test shows that with no cookie configured the HTTP/3 reply is unchanged.
- [ ] The library's `CLAUDE.md` Cookies paragraph is true of the code.
- [ ] Build clean and fast tests green.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
