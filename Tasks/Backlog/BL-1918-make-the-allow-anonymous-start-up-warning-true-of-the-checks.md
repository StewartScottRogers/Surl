---
id: BL-1918
title: Make the --allow-anonymous start-up warning true of the checks that stay
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console.UnitTests, Application/Surl.Api.UnitTests, README.md, Documentation/Planning/Decisions/ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1918 — Make the --allow-anonymous start-up warning true of the checks that stay

## Goal

The `--allow-anonymous` start-up warning, its doc comment and `README.md` no longer say that every request and login is accepted without checking credentials.

## Context

- Found by BL-1914, which rewrote the option's explanation (`AllowAnonymousExplanation` in
  `Application/Surl.Cli.UnitLibrary/CommandLineOptions.cs`) to list the checks and refusals that stay.
- `AuthenticationComposition.WriteLooseningWarnings` writes
  `--allow-anonymous: every request and login is accepted without checking credentials`. That is not true of, for
  example, an unsigned S3 write, an OCI push, a Cloud Storage signature or a Kerberos ticket under `--keytab`.
- The same sentence is pinned in `AiHelpExamples`, several unit tests, `SurlCommandLine.AllowAnonymous`'s doc comment
  and `README.md`. ADR-0032 section 9 holds the warning's wording: amend it with the change.
- Documentation and a log line only: no store, event or fault changes.

## Acceptance criteria

- [ ] The warning, `AiHelpExamples`, `SurlCommandLine.AllowAnonymous`'s doc comment and `README.md` say only what the code does.
- [ ] The pinned warning lines in the unit tests match.
- [ ] ADR-0032 records the new wording.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
