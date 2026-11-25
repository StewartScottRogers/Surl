---
id: BL-1680
title: Say in surl --aihelp that the HTTP server sets the --set-cookie cookies
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-015
created: 2026-11-24
completed: 2026-11-24
---
# BL-1680 — Say in surl --aihelp that the HTTP server sets the --set-cookie cookies

## Goal

The `--set-cookie` paragraph of `surl --aihelp` says what this build does: the HTTP server sets the cookies.

## Context

`Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`, the `--set-cookie` paragraph, ends "This build reads and checks the
option; the HTTP server does not set the cookies until Surl.Composition passes them on (BL-1670)." That is false since
BL-1673 wired them (and BL-1670 is another task), and BL-1662's `UpstreamCurlExchangesCookiesWithSurlTests` proves it
over HTTP/1.1. HTTP/3 does not set them until BL-1674 and BL-1679 are done: say whichever is true when this task runs.

## Acceptance criteria

- [x] The `--set-cookie` paragraph no longer says the HTTP server does not set the cookies, names no task ID, and says
      which HTTP versions set them.
- [x] `dotnet test Application/Surl.Cli.UnitTests` is green.

## Notes

- The paragraph (`AiHelpProse.cs`, `http` topic) now says HTTP/1.x and HTTP/2 (`h2c` included) set the cookies and
  HTTP/3 does not: true of this checkout, where `Http3ProtocolServer` is given no cookies (BL-1674 was still in Doing).
  Whoever wires HTTP/3 must change that sentence.
- "Every reply" was too wide: `Http1ConnectionServer` refuses a malformed head, and `HttpProtocolServer` a connection
  past the limits, without reaching `CookieHandler`. The paragraph now says those refusals carry none.
- Also corrected the same stale claim in `Application/Surl.Cli.UnitLibrary/CLAUDE.md`.
- Verify: build 0 warnings; fast tests green (`Surl.Cli.UnitTests` 1960 passed). `dotnet format --verify-no-changes`
  reports end-of-line and import-order errors in files this task did not change, outside `touches`; they were left alone.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. surl --aihelp now says the HTTP server sets the --set-cookie cookies over HTTP/1.x and HTTP/2, not over HTTP/3
