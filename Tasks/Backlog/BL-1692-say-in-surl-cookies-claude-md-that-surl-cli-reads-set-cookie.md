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
completed:
---
# BL-1692 — Say in Surl.Cookies' CLAUDE.md that Surl.Cli reads --set-cookie

## Goal

`Foundation/Surl.Cookies.UnitLibrary/CLAUDE.md` says where the `--set-cookie` option, `-K` and `--aihelp` live as they are now.

## Context

The file lists "the `--set-cookie` option, `-K` and `--aihelp` (BL-1661)" under "Not here yet". BL-1661 is Done, and
`Application/Surl.Cli.UnitLibrary/ConfiguredCookieReader.cs` reads the option (ADR-0189 decision 7). The sentence about
Surl keeping no cookie jar is still true and stays. Found while delivering BL-1691.

## Acceptance criteria

- [ ] `Foundation/Surl.Cookies.UnitLibrary/CLAUDE.md` calls nothing from BL-1661 pending, and says `Surl.Cli.UnitLibrary` reads `--set-cookie`.

## Notes

## Log

- 2026-11-25: Created.
