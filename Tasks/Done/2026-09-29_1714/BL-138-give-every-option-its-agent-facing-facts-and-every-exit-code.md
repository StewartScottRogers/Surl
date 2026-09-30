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
completed: 2026-09-29
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

- [x] Every `CommandLineOptions.All` row carries (or derives) argument type, allowed values
      and loosens-security exactly as the ADR's decision 5 vocabulary says; the four
      `testing` options are marked as the ADR words it.
- [x] A test in `Surl.Cli.UnitTests` (named as the ADR's decision 9 names it) fails when any
      option row lacks an argument type or, for an option with an argument, its allowed
      values, and one asserts that the options marked as loosening are exactly the set the
      ADR names.
- [x] The exit-code guidance table exists in the file and type the ADR names, and a test
      asserts it has exactly one row per `Enum.GetValues<SurlExitCode>()` member, each with a
      non-empty meaning and next step.
- [x] Tests pin the allowed-values text of `--log-level`, `--tls-max`, `--auth`, one
      `<seconds>`, one `<bytes>` and one `<number>` option against what
      `OptionArgumentReader` accepts and refuses at the edges.
- [x] `HelpTextTests` passes unchanged: `git diff Surl.Cli.UnitTests/HelpTextTests.cs` is empty.
- [x] `dotnet build Surl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test
      Surl.Cli.UnitTests --filter "TestCategory!=Integration"` passes; no test needs
      `TestCategory=Integration`; `Surl.Cli.UnitLibrary` stays at 100% line and branch
      coverage (`Measure-CodeQuality.ps1`).

## Notes

- Built ADR-0046 decisions 5, 6 and 9 as written. `OptionArgumentType(Name, AllowedValues)`
  holds the vocabulary; `OptionArgumentReading<T>(Read, Type)` takes over the `ReadArgument<T>`
  delegate `CommandLineOptions` declared privately; `OptionArgumentReader` exposes one pairing
  per read method (`Seconds`, `Number`, `Bytes`, `Path`, `Text`, `TlsVersion`, `CertificateType`,
  `KeyType`, `LogLevelWord`, `Account`, `AuthenticationMethods`), the last built from
  `AuthenticationMethodWords` so the text cannot drift from the reader.
- `CommandLineOption` gains the positional `ArgumentType` (every row must give one): `Flag`
  derives it from `Negatable`, `WithArgument<T>` from the pairing, and the `help`, `version`
  and `manual` rows give `OptionArgumentType.OptionalSubject` / `None`. The `aihelp` row and
  its `optional topic` type are BL-141's, not added here.
- Choice: the kind-level types (`OptionalSubject`, `None`, `NotNegatableFlag`,
  `NegatableFlag(longName)`) live as static members of `OptionArgumentType`, since no reader
  owns them; the ADR names no home for them.
- The `limits` suffix and the loosens-security column stay derived from categories (BL-139),
  so no field was added for them; tests pin the `testing` four and the six other `security`
  options.
- `HelpCategory` gains `Schemes` (six protocol categories, empty for the rest); `--help`
  does not read it, and `HelpTextTests.cs` is unchanged.
- Exit-code rows are the ADR's table, rechecked against the code: 2 is `RefusedOption`,
  `NoUrlSpecified`, the malformed user file (`AuthenticationComposition`) and the missing
  `--cacert` (`CommandLineRunner`); 23 includes the log-file open failure; 6 and 45 come from
  `ServingEngine`'s bind failures. No word needed changing.
- Checked: `dotnet build` clean, all fast tests green (Surl.Cli.UnitTests 579),
  `Measure-CodeQuality.ps1 -Library surl.cli*`: 100% line, 100% branch, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Every option row carries its argument type and allowed values, protocol categories carry their schemes, and ExitCodeGuidanceTable holds one row per SurlExitCode
