---
id: BL-1659
title: Build Surl.Cookies: write Set-Cookie fields with every attribute curl parses and read Cookie headers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1650]
touches: [Foundation/Surl.Cookies.UnitLibrary, Foundation/Surl.Cookies.UnitTests]
requirement: FR-015
created: 2026-11-24
completed:
---
# BL-1659 — Build Surl.Cookies: write Set-Cookie fields with every attribute curl parses and read Cookie headers

## Goal

`Surl.Cookies.UnitLibrary` holds the cookie types BL-1650's ADR names: a cookie with its
attributes written as a `Set-Cookie` field value, and a reader for a request's `Cookie`
header, each unit tested.

## Context

The project has a `.csproj` and a `CLAUDE.md` but no source; its `CLAUDE.md` describes a
cookie engine that does not exist (audit of 2026-11-24). BL-1650 decides the shape; this
task builds only the library. Wiring into the HTTP server is BL-1660.

## Acceptance criteria

- [ ] The types BL-1650's ADR names exist in `Foundation/Surl.Cookies.UnitLibrary`, with
      XML doc comments citing the ADR and https://curl.se/docs/http-cookies.html (8.21.0).
- [ ] Unit tests in `Foundation/Surl.Cookies.UnitTests` pin the `Set-Cookie` bytes for
      each attribute and the ADR's measured case byte for byte.
- [ ] A `Cookie` header with several pairs, an empty value and stray whitespace is read
      into name and value pairs as the ADR decides, each pinned by a named test.
- [ ] `Foundation/Surl.Cookies.UnitLibrary/CLAUDE.md` states what the library holds now
      and what it does not (no jar, no HTTP wiring).
- [ ] 100% line and branch coverage for the library; `dotnet build` clean and
      `dotnet test --filter "TestCategory!=Integration"` green.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
