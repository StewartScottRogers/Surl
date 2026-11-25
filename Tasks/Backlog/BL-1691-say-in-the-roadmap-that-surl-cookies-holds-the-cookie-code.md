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
completed:
---
# BL-1691 — Say in the roadmap that Surl.Cookies holds the cookie code

## Goal

The roadmap's Phase 1 status is true of `Foundation/Surl.Cookies.UnitLibrary` as it is now.

## Context

`Documentation/Planning/Roadmap.md`, "Milestone 1", says `Surl.Cookies` holds no code yet (2026-09-30). The library
now holds `CookieDefinition`, `CookieDefinitionReader`, `CookieHeaderReader`, `CookiePair` and
`SetCookieHeaderWriter` (ADR-0189). Found while delivering BL-1689.

## Acceptance criteria

- [ ] No statement in `Documentation/Planning/Roadmap.md` says `Surl.Cookies` holds no code, and the Milestone 1 status names ADR-0189.

## Notes

## Log

- 2026-11-25: Created.
