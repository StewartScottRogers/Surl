---
id: BL-1673
title: Wire --set-cookie cookies from SurlCommandLine into the HTTP server in Surl.Composition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1661]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-015
created: 2026-11-24
completed:
---
# BL-1673 — Wire --set-cookie cookies from SurlCommandLine into the HTTP server in Surl.Composition

## Goal

`surl --set-cookie` makes the HTTP server set the cookie: `Surl.Composition` passes `SurlCommandLine.Cookies` to `HttpProtocolServer`.

## Context

ADR-0189 decision 7; BL-1661 reads the option into `SurlCommandLine.Cookies` (`CookieDefinition` list), BL-1660 added the `HttpProtocolServer` constructor taking the list. `ProtocolServerRegistrations.cs:46` still uses the constructor without cookies. Add `Cookies` to `ComposedProtocolServices` like `AdvertisesHttp3`. `Surl.Api` (`HttpServerOptions.Cookies`) and Surl.Mcp spellings are separate follow-ups.

## Acceptance criteria

- [ ] A composition test starts the HTTP server with one cookie and sees its `Set-Cookie` line in a reply.
- [ ] With no `--set-cookie` the reply is unchanged.
- [ ] Build clean and fast tests green.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
