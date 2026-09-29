---
id: BL-138
title: Give every option its agent-facing facts and every exit code its next step in Surl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-137]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: FR-035
created: 2026-09-29
completed:
---
# BL-138 — Give every option its agent-facing facts and every exit code its next step in Surl.Cli

## Goal

Every row of `CommandLineOptions` carries the facts `--aihelp` shows - argument type,
allowed values, whether it loosens security - in the one table `--help` already reads, and
`Surl.Cli.UnitLibrary` holds one guidance row per `SurlExitCode` member (meaning and what an
agent should do next), exactly as BL-137's ADR decides, with nothing yet printed.

## Context

- Decision: BL-137's ADR (expected ADR-0046), decisions 5 (columns and vocabularies), 6
  (where each fact lives) and 9 (completeness tests). Build its shapes exactly; where the ADR
  derives a column from an existing field instead of adding one, derive it.
- Option table: `Surl.Cli.UnitLibrary/CommandLineOptions.cs` (rows built with `Flag(...)`,
  `WithArgument<T>(...)` and the `new(...)` rows for `help`, `version`, `manual`), each with an
  `OptionHelp` (`Surl.Cli.UnitLibrary/OptionHelp.cs`). The argument readers in
  `Surl.Cli.UnitLibrary/OptionArgumentReader.cs` are the truth for each argument type and
  its allowed values (ranges, byte suffixes, `--log-level` words, `--tls-max` versions,
  `--cert-type`/`--key-type` formats, `OptionArgumentReader.AuthenticationMethodWords`):
  every allowed-values text is checked against the reader, not copied from an ADR.
- Exit codes: `Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs` (already referenced by
  `Surl.Cli.UnitLibrary`). Meanings must agree with `ManualText.cs`'s `EXIT CODES` section
  and with where `Surl.Console/CommandLineRunner.cs` actually returns each code.
- If the ADR gives `HelpCategory` its schemes (`http` claims `http` and `https`, `gopher`
  claims `gopher` and `gophers`, `mqtt` claims `mqtt` and `mqtts`), add them here.
- `--help` output must not change in this task: the existing pinned pages in
  `Surl.Cli.UnitTests/HelpTextTests.cs` stay green unedited.
- Enumerate `SurlExitCode` with `Enum.GetValues<SurlExitCode>()` (AOT-safe); no reflection
  over the option table.

## Acceptance criteria

- [ ] Every `CommandLineOptions.All` row carries (or derives) argument type, allowed values
      and loosens-security exactly as the ADR's decision 5 vocabulary says; the four
      `testing` options are marked as the ADR words it.
- [ ] A test in `Surl.Cli.UnitTests` (named as the ADR's decision 9 names it) fails when any
      option row lacks an argument type or, for an option with an argument, its allowed
      values, and one asserts that the options marked as loosening are exactly the set the
      ADR names.
- [ ] The exit-code guidance table exists in the file and type the ADR names, and a test
      asserts it has exactly one row per `Enum.GetValues<SurlExitCode>()` member, each with a
      non-empty meaning and next step.
- [ ] Tests pin the allowed-values text of `--log-level`, `--tls-max`, `--auth`, one
      `<seconds>`, one `<bytes>` and one `<number>` option against what
      `OptionArgumentReader` accepts and refuses at the edges.
- [ ] `HelpTextTests` passes unchanged: `git diff Surl.Cli.UnitTests/HelpTextTests.cs` is empty.
- [ ] `dotnet build Surl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test
      Surl.Cli.UnitTests --filter "TestCategory!=Integration"` passes; no test needs
      `TestCategory=Integration`; `Surl.Cli.UnitLibrary` stays at 100% line and branch
      coverage (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
