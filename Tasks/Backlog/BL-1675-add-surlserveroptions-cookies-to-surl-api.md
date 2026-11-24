---
id: BL-1675
title: Add SurlServerOptions.Cookies to Surl.Api
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1673]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: FR-015
created: 2026-11-24
completed:
---
# BL-1675 — Add SurlServerOptions.Cookies to Surl.Api

## Goal

A test using `Surl.Api` sets cookies through a typed option and reads the cookies clients send back.

## Context

ADR-0189 decision 1 names the property `HttpServerOptions.Cookies`; `Surl.Api` has no `HttpServerOptions`, its options class is `Application/Surl.Api/SurlServerOptions.cs`. Add the list there, each entry the same text `--set-cookie` takes (as `Shares` and `Topics` are), default empty, and record the naming difference as an ADR-0189 amendment. The `Surl.Composition` wiring is BL-1673.

## Acceptance criteria

- [ ] A named test in `Surl.Api.UnitTests` starts an HTTP server with one entry in `Cookies` and sees its `Set-Cookie` line.
- [ ] A named test shows a bad entry refused with the `--set-cookie` text.
- [ ] A named test shows that with no entry the reply carries no `Set-Cookie`.
- [ ] ADR-0189 states the property's real name.
- [ ] Build clean and fast tests green.

## Notes

## Log

- 2026-11-24: Created.
