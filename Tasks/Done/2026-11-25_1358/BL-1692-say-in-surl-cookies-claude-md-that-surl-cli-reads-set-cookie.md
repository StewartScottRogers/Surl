---
id: BL-1692
title: Say in Surl.Cookies' CLAUDE.md that Surl.Cli reads --set-cookie
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Cookies.UnitLibrary/CLAUDE.md]
requirement: FR-015
created: 2026-11-25
completed: 2026-11-25
---
# BL-1692 — Say in Surl.Cookies' CLAUDE.md that Surl.Cli reads --set-cookie

## Goal

`Foundation/Surl.Cookies.UnitLibrary/CLAUDE.md` says where the `--set-cookie` option, `-K` and `--aihelp` live as they are now.

## Context

The file lists "the `--set-cookie` option, `-K` and `--aihelp` (BL-1661)" under "Not here yet". BL-1661 is Done, and
`Application/Surl.Cli.UnitLibrary/ConfiguredCookieReader.cs` reads the option (ADR-0189 decision 7). The sentence about
Surl keeping no cookie jar is still true and stays. Found while delivering BL-1691.

## Acceptance criteria

- [x] `Foundation/Surl.Cookies.UnitLibrary/CLAUDE.md` calls nothing from BL-1661 pending, and says `Surl.Cli.UnitLibrary` reads `--set-cookie`.

## Notes

- Delivered by `align-and-document`. The "Not here yet" section is now "Not here": no cookie jar is planned (ADR-0189
  decision 4), so "yet" would be false. It says `Surl.Cli.UnitLibrary` reads `--set-cookie` (also from `-K`, at most 64)
  and holds its `--aihelp` facts, and that `CookieHandler` in `Surl.Protocol.Http.UnitLibrary` matches a cookie's scope,
  adds the reply field and publishes `CookieReceivedEvent`.
- No follow-up filed: `ManualText` has no section on `--set-cookie`, and no rule requires one per option.
- No `.cs` or project file changed, so `/verify` was not needed. `dotnet build`: 0 warnings, 0 errors. Fast tests:
  83 assemblies, 28945 passed, 0 failed, 3 skipped.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. Surl.Cookies' CLAUDE.md now says Surl.Cli reads --set-cookie and calls nothing from BL-1661 pending
