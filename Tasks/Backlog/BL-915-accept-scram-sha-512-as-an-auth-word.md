---
id: BL-915
title: Accept scram-sha-512 as an --auth word
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-700]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0125-sasl-scram-sha-1-and-scram-sha-256-in-surl-authentication.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-915 — Accept scram-sha-512 as an --auth word

## Goal

`surl --auth scram-sha-512` accepts SASL `SCRAM-SHA-512` (`AuthenticationMethod.ScramSha512`, added by
BL-700), so the servers that offer SCRAM through the policy - the Kafka broker first (ADR-0105 decision 5) - can
offer it.

## Context

- BL-700 added `AuthenticationMethod.ScramSha512` and the `SCRAM-SHA-512` mechanism to `Surl.Authentication`,
  offered after `SCRAM-SHA-256` and before `SCRAM-SHA-1`, not in the default set. It stayed inside its library,
  so no `--auth` word turns it on yet.
- Where the words live: `Application/Surl.Composition.UnitLibrary/AuthenticationComposition.cs`
  (`MethodsByWord`), `Application/Surl.Cli.UnitLibrary/OptionArgumentReader.cs` (the allowed words, in
  `AuthenticationMethod` order: `scram-sha-256`, `scram-sha-512`, `scram-sha-1`), `CommandLineOptions.cs`,
  `ManualText.cs`, `AiHelpProse.cs`, and their pinned tests (`AiHelpFactsTests`, `CommandLineParserTests`,
  `HelpTextTests`, `ManualTextTests`, `CommandLineRunnerAuthenticationTests`).
- ADR-0125 records `SCRAM-SHA-1` and `SCRAM-SHA-256`; amend it (marked "Decided by Claude under Stewart's
  delegation") with `SCRAM-SHA-512`, its offer order and its word.

## Acceptance criteria

- [ ] `CommandLineRunnerAuthenticationTests` maps `scram-sha-512` to `AuthenticationMethod.ScramSha512`, and the
      mail offer with every word accepted lists `SCRAM-SHA-256`, `SCRAM-SHA-512`, `SCRAM-SHA-1` in that order.
- [ ] `--help`, `--manual` and `--aihelp` list `scram-sha-512` after `scram-sha-256`, and the completeness tests pass.
- [ ] ADR-0125 has an amendment recording `SCRAM-SHA-512` (BL-700) and its `--auth` word.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Filed by BL-700, which stayed inside `Surl.Authentication` (its `touches`); `Surl.Cli` and `Surl.Composition`
  were held by BL-548 and BL-803 at the time.

## Log

- 2026-11-06: Created.
