---
id: BL-1676
title: Add the cookies argument to Surl.Mcp's start_server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1675]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Documentation/Planning/Decisions/ADR-0189-the-http-server-sets-cookies-from-set-cookie-options-and-publishes-the-cookies-curl-sends-back.md, Documentation/Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md]
requirement: FR-015
created: 2026-11-24
completed: 2026-11-25
---
# BL-1676 — Add the cookies argument to Surl.Mcp's start_server

## Goal

`surl-mcp`'s `start_server` takes a `cookies` array, each entry the text `--set-cookie` takes.

## Context

ADR-0189 decision 1. `Application/Surl.Mcp.UnitLibrary/StartServerTool.cs` holds the input schema; today a cookie is only reachable as `--set-cookie` inside `arguments`. Pass the entries to `SurlServerOptions.Cookies` (BL-1675). Decision 1 also names a "control endpoint start body field `cookies`": check whether `Surl.Control` starts servers at all, and if it does not, amend ADR-0189 to say so.

## Acceptance criteria

- [x] A named test in `Surl.Mcp.UnitTests` starts a server with `cookies` and sees the cookie set.
- [x] A named test shows a bad entry refused with the `--set-cookie` text.
- [x] The `start_server` input schema lists `cookies`.
- [x] ADR-0189's control-endpoint sentence is true of the code.
- [x] Build clean and fast tests green.

## Notes

- `cookies` is a schema array of at most 64 strings, passed to `SurlServerOptions.Cookies`; refusals are `surl`'s.
- The control endpoint starts no server (`Surl.Control`'s routes all drive a running one), so ADR-0189 has amendment 2
  dropping its "start body field `cookies`".
- Review found that `cookies` opened a bypass of ADR-0103 decision 4: an option at the end of `listen_urls` took the
  composed `--set-cookie` as its value, leaving the cookie as an unchecked word. Reproduced with failing tests, then
  fixed: `listen_urls` entries starting with `-` are refused (ADR-0103 amendment 3, Claude's decision). ADR-0103 was
  added to `touches`; no other task in Doing names it.
- Pipeline: the change is a few lines, so plan, tests, implementation and documentation were done in the session; only
  the review was delegated (`code-reviewer`). No conformance stage: no byte or exit code `surl` sends changes, and
  BL-1662 holds the pinned-curl proof of `--set-cookie`.
- The HTTP/3 server is not yet given the cookies; already filed as BL-1679.

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. surl-mcp start_server takes cookies; listen_urls entries starting with - are refused, closing the bypass review found
