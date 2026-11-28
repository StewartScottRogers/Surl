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
completed: 2026-11-28
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

- [x] NFR-006 says what `Test-PublishedSurl.ps1` allows in the publish directory.
- [x] NFR-002, NFR-003 and the root `CLAUDE.md` "Quality gates" section name `Surl.Api` beside `Surl.Console` as gated.
- [x] The glossary no longer calls `Surl.Api` planned.
- [x] The `SurlServerComposition` doc comment names the hosts that call it as the code does.
- [x] `dotnet build` has 0 warnings and the fast tests pass.

## Notes

- `surl` and `Surl.Api` call `SurlServerComposition` directly; `surl-mcp` reaches it through `Surl.Api` (`Surl.Mcp` references `Surl.Mcp.UnitLibrary`, which starts servers with `SurlServer.StartAsync` in `RunningSurlServers.cs`). The doc comment and the glossary row say so.
- NFR-006 names the `release.yml` publish switches because a plain `dotnet publish` is not what the one-file check runs against. No publish was run for this task; the wording rests on `Test-PublishedSurl.ps1` check 1 and the `release.yml` comment.
- The `align-and-document` subagent left the root `CLAUDE.md` unedited, because it would not change that file on a delegated request. The lane session made that one-sentence edit: the task's acceptance criteria and `touches` name it.
- Verified 2026-11-28: `dotnet build` 0 warnings, fast tests 0 failures, `dotnet format --verify-no-changes` clean on the changed `.cs` file.
- Out-of-scope statements found are filed as BL-2112 (stop check, native publish, gate scope wording) and BL-2111 (`Surl.Mcp` is not in the quality gate).

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. Requirements.md, CLAUDE.md, the glossary and the SurlServerComposition doc comment match the scripts and code on Surl.Api and the publish output
