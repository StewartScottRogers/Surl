---
id: BL-1691
title: Say in the roadmap that Surl.Cookies holds the cookie code
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Roadmap.md]
requirement: FR-015
created: 2026-11-25
completed: 2026-11-25
---
# BL-1691 — Say in the roadmap that Surl.Cookies holds the cookie code

## Goal

The roadmap's Phase 1 status is true of `Foundation/Surl.Cookies.UnitLibrary` as it is now.

## Context

`Documentation/Planning/Roadmap.md`, "Milestone 1", says `Surl.Cookies` holds no code yet (2026-09-30). The library
now holds `CookieDefinition`, `CookieDefinitionReader`, `CookieHeaderReader`, `CookiePair` and
`SetCookieHeaderWriter` (ADR-0189). Found while delivering BL-1689.

## Acceptance criteria

- [x] No statement in `Documentation/Planning/Roadmap.md` says `Surl.Cookies` holds no code, and the Milestone 1 status names ADR-0189.

## Notes

- Delivered by `align-and-document`: only the Milestone 1 status line changed. It claims pinned-curl proof for `http` over
  HTTP/1.1 (BL-1662) and HTTP/3 (BL-1689) only, so it stays true whether or not BL-1690 (in Doing on another lane) lands.
- No `.cs` or project file changed. `dotnet build`: 0 warnings, 0 errors; fast tests passed.
- Follow-up filed with the board script (a one-line docs task, so no `task-planner` run): BL-1692, the stale
  "Not here yet" paragraph in `Surl.Cookies.UnitLibrary/CLAUDE.md`, outside this task's `touches`.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The roadmap's Phase 1 status says what Surl.Cookies and the HTTP server do with cookies, citing ADR-0189
