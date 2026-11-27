---
id: BL-1958
title: Make the remaining --allow-anonymous summaries true of the checks that stay
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Api, Foundation/Surl.Authentication.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1958 — Make the remaining --allow-anonymous summaries true of the checks that stay

## Goal

The short descriptions of `--allow-anonymous` no longer say that any or every login is accepted unchecked.

## Context

- Found by BL-1918, which rewrote the start-up warning (ADR-0032 section 9) to say `most requests and logins`.
- Still unqualified: the short help `Accept any login, or none (warns)` in
  `Application/Surl.Cli.UnitLibrary/CommandLineOptions.cs` (pinned in `HelpTextTests`), the doc comment on the
  anonymous option in `Application/Surl.Api/SurlServerOptions.cs`, and the `AllowAnonymous` parameter comment in
  `Foundation/Surl.Authentication.UnitLibrary/AuthenticationSettings.cs`.
- The short help must still fit curl's 79 columns (ADR-0034). The checks that stay are listed in
  `AllowAnonymousExplanation`.
- Help text and doc comments only: no behaviour, store, event or fault changes.

## Acceptance criteria

- [ ] The short help, both doc comments and the pinned `HelpTextTests` lines say only what the code does.
- [ ] `surl --help --allow-anonymous` shows the new short description in lines of at most 79 columns.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
