---
id: BL-1685
title: Say 'extended servers on/off' in place of the shorthand 'extensions on/off'
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests, Application/Surl.Composition.UnitLibrary/CLAUDE.md, Application/Surl.Composition.UnitTests, Documentation/Planning/Decisions/ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md, Documentation/Product/Requirements.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1685 — Say 'extended servers on/off' in place of the shorthand 'extensions on/off'

## Goal

No live file says "extensions on" or "extensions off" (or "extensions are on/off"); each says
"extended servers on/off" (or "while the extended servers are on/off"), so the only
vocabulary for the Curl port's added protocols is "extended protocol" and "extended server".

## Context

Stewart, 2026-11-25: use "extended protocols" consistently, and reword the shorthand. The
Glossary (`Documentation/Wiki/Glossary.md`) defines "extended protocol", "extended server"
and "extensions switch" (`SURL_EXTENSIONS`). The switch's name and the environment variable
stay as they are; only prose saying "extensions on/off" changes.

Files found 2026-11-25 (`git grep -i -E "extensions (are )?(on|off)\b"`, excluding `Tasks/`):
- `Application/Surl.Api.UnitTests/SurlServerTests.ProtocolServers.cs` (a `DisplayName`)
- `Application/Surl.Api/README.md`
- `Application/Surl.Api/SurlServerArguments.cs` (two `<param>` doc comments)
- `Application/Surl.Composition.UnitLibrary/CLAUDE.md`
- `Application/Surl.Composition.UnitTests/StartTestSeams.cs` (a `<summary>`)
- `Documentation/Planning/Decisions/ADR-0083-...md` (decision text; an ADR wording fix, not a
  change of decision)
- `Documentation/Product/Requirements.md` (FR-056)

Archived tasks under `Tasks/Done/` are final and are not edited.

## Acceptance criteria

- [x] `git grep -i -E "extensions (are )?(on|off)\b" -- . ':!Tasks'` finds nothing.
- [x] No identifier, environment variable or `--aihelp` text changes meaning; `SURL_EXTENSIONS`
      and the Glossary's "extensions switch" row stay.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-11-25: Reworded the eight phrases in the seven files listed under Context. Only prose, XML
  doc comments and one test `DisplayName` changed; identifiers such as `AreExtensionsTurnedOn`
  stay, as the Goal covers prose only.
- In ADR-0083 decision 8 the sentence now reads "While the extended servers are on and at least
  one is registered", so "extended server" is not said twice. The decision is unchanged.
- Verified: build 0 warnings, 0 errors; fast tests 28945 passed, 0 failed, 3 skipped.
- `dotnet format --verify-no-changes` reports only `ENDOFLINE`, all in files this task did not
  edit. The one sampled (`TelnetProtocolServer.cs`) is LF in this lane's working tree and LF in
  the index (`text=auto`), so nothing was reformatted here.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. Live files say 'extended servers on/off'; no 'extensions on/off' remains outside Tasks, build clean and fast tests green
