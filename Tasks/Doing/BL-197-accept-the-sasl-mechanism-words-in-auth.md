---
id: BL-197
title: Accept the SASL mechanism words in --auth
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-196]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-046
created: 2026-09-29
completed:
---
# BL-197 — Accept the SASL mechanism words in --auth

## Goal

`--auth` accepts the SASL mechanism words BL-185's ADR decides (or maps the mechanisms onto the
existing words as it decides), `Surl.Console` composes them into the authentication policy, and
the help, manual, AI help and start-up warning describe them.

## Context

- Decisions: BL-185's ADR (words, default set, order, descriptions); ADR-0032 sections 1, 3 and 9
  (the `--auth` rules, the method order, the warning line `surl: warning: --auth: accepted
  methods are <methods>`); ADR-0034 (help); ADR-0046 (AI help facts).
- Code: `Surl.Cli.UnitLibrary/CommandLineOptions.cs` (the `--auth` row and its
  `GivenAuthenticationMethods`), `ManualText.cs`, `AiHelpProse.cs`;
  `Surl.Console/AuthenticationComposition.cs` (maps words to `Surl.Authentication` methods).
- This task lands after BL-194 to BL-196 so every word it accepts is implemented; no "not
  available in this build" refusal is needed.
- If BL-185's ADR decided no new word, this task still aligns the `--auth` description, manual and
  AI help with which mechanisms each existing word enables; say so in Notes.

## Acceptance criteria

- [ ] `CommandLineParserTests` cover each new word (any case), a repeated word, and an unknown
      word refused with `option --auth: is badly used here`.
- [ ] A `Surl.Console.UnitTests` test shows each word enabling its mechanism in the composed
      policy, and the warning line listing the accepted set in ADR-0032 section 3's order.
- [ ] `HelpTextTests`, `ManualTextTests`, `AiHelpFactsTests` and `AiHelpTextTests` pass with the
      updated texts.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
