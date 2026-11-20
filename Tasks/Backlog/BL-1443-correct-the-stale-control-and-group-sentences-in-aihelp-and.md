---
id: BL-1443
title: Correct the stale --control and --group sentences in --aihelp and --manual
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary/AiHelpProse.cs, Application/Surl.Cli.UnitLibrary/ManualText.cs, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1443 — Correct the stale --control and --group sentences in --aihelp and --manual

## Goal

`surl --aihelp control`, `surl --aihelp auth` and `surl --manual` stop saying the control endpoint is
not served and that nothing uses `--group`. Both are false today.

## Context

- Surly bug report `2026-11-19T1942-Surl-bug-report.md` items 5 and 11 (Surl `763754785`, unchanged
  since).
- Item 5: `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs:97` says "This build reads and checks both
  options; it does not yet serve the endpoint or read the token file." and `ManualText.cs:554` says
  "checks both options but does not yet serve the endpoint". Both are stale. BL-977 serves the endpoint
  (`ComposedServing.ServeAsync`, `ControlComposition`), BL-972 reads the token file, BL-816 serves it
  from `Surl.Api` (`SurlServer.ControlUrl`), and BL-1125 already corrected `--help`. The Surl.Api
  README is right.
- Item 11: `AiHelpProse.cs:70` says "No option takes a member list yet, so today a group is only checked
  at start and nothing uses it", and `ManualText.cs:221-222` says the same. `--share-access` and
  `--topic-access` take `@group` members (ADR-0102 decisions 2.2 and 3.6; `ContentShareComposition`,
  `MessageBusComposition`), and so does `--shared-folder`, as `--aihelp` itself says at line 71.
- `Application/Surl.Cli.UnitTests/ManualTextTests.cs:230-231` and `:563` pin the stale manual lines.

## Acceptance criteria

- [ ] The control topic's last paragraph states that surl serves the endpoint on the control listen URL,
      reads the token file at start, and writes `Control endpoint listening on <url>` after the
      `Listening on` lines, and names where the routes are documented. It says nothing the code does not
      do.
- [ ] The `--group` paragraph names the options that take `@<group>` members (`--share-access`,
      `--topic-access`, `--shared-folder`) instead of saying nothing uses a group.
- [ ] `ManualText.cs` says the same in both places, and `ManualTextTests` pins the new lines.
- [ ] `grep -r "does not yet serve the endpoint" Application` and `grep -r "nothing uses it" Application`
      find nothing.
- [ ] `AiHelpTextTests`, `CommandLineRunnerAiHelpTests` and `ManualTextTests` are green, and
      `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T1942-Surl-bug-report.md` items 5 and 11.
