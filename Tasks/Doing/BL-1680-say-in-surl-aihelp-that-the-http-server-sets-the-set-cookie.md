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
completed:
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

- [ ] The `--set-cookie` paragraph no longer says the HTTP server does not set the cookies, names no task ID, and says
      which HTTP versions set them.
- [ ] `dotnet test Application/Surl.Cli.UnitTests` is green.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
