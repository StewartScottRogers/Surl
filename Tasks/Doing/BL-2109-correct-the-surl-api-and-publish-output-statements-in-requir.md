---
id: BL-2109
title: Correct the Surl.Api and publish-output statements in Requirements.md, CLAUDE.md, the glossary and SurlServerComposition
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md, CLAUDE.md, Documentation/Wiki/Glossary.md, Application/Surl.Composition.UnitLibrary/SurlServerComposition.cs]
requirement: none
created: 2026-11-28
completed:
---
# BL-2109 — Correct the Surl.Api and publish-output statements in Requirements.md, CLAUDE.md, the glossary and SurlServerComposition

## Goal

The statements listed in Context are true of the scripts and code as they are now. No behaviour changes.

## Context

Found while working BL-2103 (checked by reading the files on 2026-11-28):

- `Documentation/Product/Requirements.md` NFR-006 says the publish produces 1 native file; `Test-PublishedSurl.ps1` (check 1) also allows the native symbol file beside `surl`. Compare `.github/workflows/release.yml` for the switches that keep library symbol and XML files out.
- NFR-002, NFR-003 and the root `CLAUDE.md` "Quality gates" section name `*.UnitLibrary` and `Surl.Console`; `Measure-CodeQuality.ps1` also gates `Surl.Api`.
- The "server composition" row of `Documentation/Wiki/Glossary.md` calls `Surl.Api` planned; it exists and starts servers through the composition (`Application/Surl.Api/SurlServer.cs`).
- The doc comment on `SurlServerComposition` says "Each host (`surl` today) calls it"; `Surl.Api` calls it too. Check whether `Surl.Mcp` reaches it through `Surl.Api` before wording it.

Stand-in: nothing to add; documentation only.

## Acceptance criteria

- [ ] NFR-006 says what `Test-PublishedSurl.ps1` allows in the publish directory.
- [ ] NFR-002, NFR-003 and the root `CLAUDE.md` "Quality gates" section name `Surl.Api` beside `Surl.Console` as gated.
- [ ] The glossary no longer calls `Surl.Api` planned.
- [ ] The `SurlServerComposition` doc comment names the hosts that call it as the code does.
- [ ] `dotnet build` has 0 warnings and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
