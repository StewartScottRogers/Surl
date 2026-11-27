---
id: BL-1815
title: Correct stale statements in the Surl.Cli and Surl.Composition CLAUDE.md files
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary/CLAUDE.md, Application/Surl.Composition.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-1815 — Correct stale statements in the Surl.Cli and Surl.Composition CLAUDE.md files

## Goal

`Application/Surl.Cli.UnitLibrary/CLAUDE.md` and `Application/Surl.Composition.UnitLibrary/CLAUDE.md` no longer describe finished work as pending or a removed refusal as present.

## Context

Found while working BL-1809. Docs only; no `.cs` file changes.

- Cli file, "Unix domain sockets" bullet: it says `UnixSocketCombination` refuses `ftp`/`ftps` "until BL-605". BL-605 is done and `UnixSocketCombination.cs` has no such refusal: `Refuse` refuses a second listen URL, a scheme in `DatagramSchemes` and `--http3`; `RefuseProxy` refuses `--proxy`.
- Cli file, other pending wording about finished tasks: the proxy readers' "their option rows are BL-1004's" (rows `proxy-user`, `proxy-user-file`, `proxy-allow-origin` are in `CommandLineOptions.cs`); "BL-653, BL-654, BL-663 and BL-666 add the other item options"; "No option row uses them yet: BL-989 adds the rows and calls `Find`; BL-988 composes them" (the `share`, `share-access` and `bucket` rows exist, `CommandLineParser` calls `ConfiguredContentConflicts.Find`, and `ContentShareComposition` composes them); "Serving the endpoint and reading the token file are BL-816's" (`Surl.Composition`'s `ControlComposition` and `ComposedServing` do it).
- Composition file, opening paragraph: it says `Surl.Console` references the library "and so will `Surl.Api` and possibly `Surl.Mcp` (BL-612)". `Application/Surl.Api/Surl.Api.csproj` already references it; `Surl.Mcp.UnitLibrary` references `Surl.Api`, and no `Surl.Mcp` project references `Surl.Composition` directly.
- BL-1810 edits another paragraph of the Cli file; leave the `ParseProxy` scheme sentence to it.

## Acceptance criteria

- [ ] The "Unix domain sockets" bullet names only the refusals `UnixSocketCombination.cs` makes, with no mention of an `ftp`/`ftps` refusal.
- [ ] The Cli file states the option rows, conflict check and composition for the proxy, item, share and bucket options as they are now, with no "No option row uses them yet" or other future wording about BL-1004, BL-653, BL-654, BL-663, BL-666, BL-988 or BL-989.
- [ ] The control endpoint bullet in the Cli file names the `Surl.Composition` types that read the token file and serve the endpoint, in place of "are BL-816's".
- [ ] The Composition file's opening paragraph says `Surl.Console` and `Surl.Api` reference the library and that `Surl.Mcp` reaches it through `Surl.Api`.

## Notes

## Log

- 2026-11-26: Created.
