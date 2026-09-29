---
id: BL-108
title: Parse --user, --user-file, --allow-anonymous, --allow-plaintext-auth, --auth and --self-signed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-100, BL-103]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: FR-014
created: 2026-09-29
completed:
---
# BL-108 — Parse --user, --user-file, --allow-anonymous, --allow-plaintext-auth, --auth and --self-signed

## Goal

`Surl.Cli` parses `--user`, `--user-file`, `--allow-anonymous`, `--allow-plaintext-auth`,
`--auth` and `--self-signed` into `SurlCommandLine` exactly as ADR-0032 decides, with their
help entries in ADR-0034's categories.

## Context

FR-014 and the new rows ADR-0032 (BL-100) adds; ADR-0032 decision 1 gives syntax, argument
kinds, defaults, negatability, repetition and every error text, decision 3 the `--auth`
words; ADR-0034 (BL-102) gives categories and descriptions. BL-103 built the categorised help
table this task adds entries to. Nothing is enforced yet: BL-116 and BL-117 compose these
values, so until then each help description must still be true (ADR-0034 says how an option
that is parsed but not yet composed is described, if at all; follow it).

- `Surl.Cli.UnitLibrary/CommandLineOptions.cs`: the table (`Flag`, `WithArgument<T>`);
  `OptionArgumentReader.cs` (`ReadPath`, `ReadText`); a new reader for `name:password` and
  for the `--auth` method list, each returning ADR-0032's refusal text.
- `Surl.Cli.UnitLibrary/SurlCommandLine.cs`: add the members ADR-0032 names (accounts from
  `--user`, the `--user-file` path as given, the three flags, the accepted-method set).
  Keep a password out of `ToString()` output (a record prints its members): override
  `PrintMembers` or hold accounts in a type whose `ToString` hides the password, and test it.
- Tests: `Surl.Cli.UnitTests/CommandLineParserTests.cs`, `HelpTextTests.cs`.

## Acceptance criteria

- [ ] `CommandLineParserTests` prove, for each option, the parsed value and each refusal
      ADR-0032 names (`--user` with no `:`, an empty name, an empty argument, an unknown
      `--auth` word, `=value` on a flag, `--no-` where not negatable), each
      `SurlExitCode.FailedInit` with ADR-0032's exact text and the `try` line.
- [ ] Tests prove the repetition rule ADR-0032 decides for `--user` and `--auth`, and that a
      password with a `:` in it (`--user a:b:c`) parses as ADR-0032 says.
- [ ] A test proves `new SurlCommandLine { ... accounts ... }.ToString()` does not contain
      the password.
- [ ] `HelpTextTests` pin the new options' help lines in ADR-0034's categories.
- [ ] `dotnet build Surl.Cli.UnitLibrary -warnaserror` is clean; the fast tests pass; 100%
      line and branch coverage kept.

## Notes

## Log

- 2026-09-29: Created.
