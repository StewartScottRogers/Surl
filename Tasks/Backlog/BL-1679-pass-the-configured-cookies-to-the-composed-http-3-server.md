---
id: BL-1679
title: Pass the configured cookies to the composed HTTP/3 server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1673, BL-1674]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-015
created: 2026-11-24
completed:
---
# BL-1679 — Pass the configured cookies to the composed HTTP/3 server

## Goal

`surl --http3 --set-cookie` sets the cookie on HTTP/3 replies too.

## Context

ADR-0189 decision 2. `SurlServerComposition.ComposeHttp3ProtocolServer` (`SurlServerComposition.Start.cs`) builds the `Http3ProtocolServer` without `SurlCommandLine.Cookies`; BL-1674 gives that server a way to take them. Update the library's `CLAUDE.md` sentence saying the HTTP/3 server does not set cookies.

## Acceptance criteria

- [ ] A composition test with `--http3` and one cookie sees its `set-cookie` field in an HTTP/3 reply.
- [ ] With no `--set-cookie` the HTTP/3 reply is unchanged.
- [ ] The library's `CLAUDE.md` is true of the code.
- [ ] Build clean and fast tests green.

## Notes

## Log

- 2026-11-24: Created.
