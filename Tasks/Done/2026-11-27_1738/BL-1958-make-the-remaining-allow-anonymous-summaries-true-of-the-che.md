---
id: BL-1958
title: Make the remaining --allow-anonymous summaries true of the checks that stay
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Api, Foundation/Surl.Authentication.UnitLibrary, Documentation/Planning/Decisions/ADR-0034-curl-style-help-categories-and-the-manual.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
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

- [x] The short help, both doc comments and the pinned `HelpTextTests` lines say only what the code does.
- [x] `surl --help --allow-anonymous` shows the new short description in lines of at most 79 columns.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Short help is now `Accept most logins or none (warns)`: `most`, as BL-1918's start-up warning says, and
  34 characters, ADR-0034 decision 2's limit for a description.
- Recorded as a dated amendment in ADR-0034 (decided by Claude under Stewart's delegation). Added
  `Documentation/Planning/Decisions/ADR-0034-curl-style-help-categories-and-the-manual.md` to `touches` for it;
  no task in `Doing` named that file.
- The `SurlServerOptions.AllowAnonymous` comment points at `surl --help --allow-anonymous` for the checks that
  stay; the `AuthenticationSettings.AllowAnonymous` comment names the ones this library makes, as examples.
- Verified 2026-11-27: build 0 warnings, format check clean, fast tests 30378 passed, 0 failed, 3 skipped;
  the option page's widest line is 79 columns.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The --allow-anonymous short help and both doc comments now say most requests or logins are accepted unchecked
