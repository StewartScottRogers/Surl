---
id: BL-1918
title: Make the --allow-anonymous start-up warning true of the checks that stay
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console.UnitTests, Application/Surl.Api.UnitTests, README.md, Documentation/Planning/Decisions/ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md, Documentation/Planning/Decisions/ADR-0046-surl-aihelp-markdown-help-for-ai-agents.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
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

- [x] The warning, `AiHelpExamples`, `SurlCommandLine.AllowAnonymous`'s doc comment and `README.md` say only what the code does.
- [x] The pinned warning lines in the unit tests match.
- [x] ADR-0032 records the new wording.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- New line: `--allow-anonymous: most requests and logins are accepted without checking credentials (see surl --help --allow-anonymous)`.
  Why: "most" is true and still warns of the risk ("some" would understate it), and the option page is the one
  place that lists the checks that stay, so the one-line warning does not repeat the list.
- Added ADR-0046 to `touches`: its example table row 17 quotes the warning. No task in `Doing` names it.
- Verified 2026-11-27: build 0 warnings, 0 errors; fast tests pass in all 83 test assemblies; `dotnet format
  --verify-no-changes` on the changed `.cs` files exits 0; `surl --help --allow-anonymous` writes the option page.
- Follow-up BL-1955: the short help, `SurlServerOptions` and `AuthenticationSettings` still say "any login" or
  "every request and login".

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The --allow-anonymous warning, doc comment, README and ADR-0032 say most requests and logins are accepted unchecked and point to surl --help --allow-anonymous.
