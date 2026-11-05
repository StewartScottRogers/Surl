---
id: BL-575
title: Add the --auth words scram-sha-1 and scram-sha-256 to Surl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-416]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-575 — Add the --auth words scram-sha-1 and scram-sha-256 to Surl.Console

## Goal

`surl --auth scram-sha-256,scram-sha-1 ...` accepts the SASL mechanisms BL-416 added, so a server that
offers SASL through `ISaslAuthenticationPolicy` offers them.

## Context

- BL-416 added `AuthenticationMethod.ScramSha256` and `AuthenticationMethod.ScramSha1` to
  `Surl.Authentication`, outside the default set. `Surl.Console`'s
  `AuthenticationComposition.MethodsByWord` has no word for them yet, so nothing can turn them on.
- The `--auth` option's `--aihelp` facts and allowed values must grow with it (root CLAUDE.md,
  "`surl --aihelp` is how an agent learns the command line").

## Acceptance criteria

- [x] `AuthenticationComposition.MethodsByWord` maps `scram-sha-1` and `scram-sha-256`, and
      `CommandLineRunnerAuthenticationTests` has a row for each.
- [x] The `--auth` option's allowed values in `--aihelp` list both words, and the completeness tests pass.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Added `Application/Surl.Cli.UnitLibrary` and `Application/Surl.Cli.UnitTests` to `touches`:
  `OptionArgumentReader.AuthenticationMethodWords` refuses any `--auth` word outside its list, and the
  help, manual and `--aihelp` texts that name the words live there, so `Surl.Console` alone cannot
  accept them. No task in Doing names either project.
- Word order (decided by Claude): `scram-sha-256`, `scram-sha-1` sit after `digest` and before
  `digest-md5`, the order `AuthenticationMethod` declares them in, which ADR-0032 section 3 makes the
  listing order. In the prose they follow `gssapi`, as in ADR-0049's offer order. They stay out of the
  default set, as BL-416 decided.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. surl --auth scram-sha-256,scram-sha-1 turns on SASL SCRAM for the mail servers; --help, --manual and --aihelp list both words
