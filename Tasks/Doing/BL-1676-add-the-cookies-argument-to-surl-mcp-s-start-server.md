---
id: BL-1676
title: Add the cookies argument to Surl.Mcp's start_server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1675]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Documentation/Planning/Decisions/ADR-0189-the-http-server-sets-cookies-from-set-cookie-options-and-publishes-the-cookies-curl-sends-back.md]
requirement: FR-015
created: 2026-11-24
completed:
---
# BL-1676 — Add the cookies argument to Surl.Mcp's start_server

## Goal

`surl-mcp`'s `start_server` takes a `cookies` array, each entry the text `--set-cookie` takes.

## Context

ADR-0189 decision 1. `Application/Surl.Mcp.UnitLibrary/StartServerTool.cs` holds the input schema; today a cookie is only reachable as `--set-cookie` inside `arguments`. Pass the entries to `SurlServerOptions.Cookies` (BL-1675). Decision 1 also names a "control endpoint start body field `cookies`": check whether `Surl.Control` starts servers at all, and if it does not, amend ADR-0189 to say so.

## Acceptance criteria

- [ ] A named test in `Surl.Mcp.UnitTests` starts a server with `cookies` and sees the cookie set.
- [ ] A named test shows a bad entry refused with the `--set-cookie` text.
- [ ] The `start_server` input schema lists `cookies`.
- [ ] ADR-0189's control-endpoint sentence is true of the code.
- [ ] Build clean and fast tests green.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing.
