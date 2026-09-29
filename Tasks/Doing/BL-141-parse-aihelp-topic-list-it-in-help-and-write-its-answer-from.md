---
id: BL-141
title: Parse --aihelp [topic], list it in --help and write its answer from surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-140]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-035
created: 2026-09-29
completed:
---
# BL-141 — Parse --aihelp [topic], list it in --help and write its answer from surl

## Goal

`surl --aihelp`, `surl --aihelp <topic>` and `surl --aihelp all` write BL-139/BL-140's
Markdown to stdout and exit `SurlExitCode.Ok` (0), and `--aihelp` is listed in `--help`,
`--help all` and its category, as BL-137's ADR decides.

## Context

- Decision: BL-137's ADR (expected ADR-0046), decisions 1 (parsing and result shape), 2 (the
  `--help` row and re-pinned pages), 8 (unknown and option-like topics) and 10 (`--manual`'s
  `SEE ALSO`).
- `Surl.Cli.UnitLibrary`: add the `--aihelp` row to `CommandLineOptions.cs` with its
  `OptionHelp` and BL-138's facts; the new `CommandLineOptionKind` member
  (`CommandLineOption.cs`); the new `CommandLineOutcome` member (`CommandLineOutcome.cs`); the
  factory and subject property on `CommandLineParseResult.cs`; reading in
  `CommandLineParser.cs`, following how `CommandLineOptionKind.Help` takes its subject.
  `ManualText.cs` changes only if the ADR's decision 10 says so.
- `Surl.Console/CommandLineRunner.cs` `RunAsync`: add the outcome to the `switch` beside
  `ShowHelp`, writing the answer's output and error as `WriteHelp` does. Both libraries change
  in this one task so no commit leaves the new outcome falling into `ServeAsync`.
- Pinned `--help` pages to update: the short list, `--help all` and `--help surl` (and any
  category the ADR adds `--aihelp` to) in `Surl.Cli.UnitTests/HelpTextTests.cs`; the parser
  tests are in `Surl.Cli.UnitTests/CommandLineParserTests.cs`; the runner's in
  `Surl.Console.UnitTests/CommandLineRunnerTests.cs`.
- Help and `--aihelp` are not log output (ADR-0033 section 1, ADR-0034 decision 3): written
  whatever `-s` or `--log-level` says.

## Acceptance criteria

- [ ] `CommandLineParserTests` pin every parsing case the ADR's decision 1 lists (at least
      `--aihelp`, `--aihelp mqtt`, `--aihelp=mqtt`, `--aihelp ""`, `--aihelp all`, an
      option before and after it, `--no-aihelp`, and `-h`/`-V`/`-M` before it), each with the
      outcome and subject or `CommandLineFailure` the ADR gives.
- [ ] `HelpTextTests` pin the new `--help` short list, `--help all` and `--help surl` pages
      with the `--aihelp` line exactly as the ADR gives it, laid out by ADR-0034's 79-column
      rule, and `--help --aihelp` answers its option page.
- [ ] `CommandLineRunnerTests` show that `RunAsync` with `["--aihelp"]`, `["--aihelp",
      "mqtt"]`, `["--aihelp", "all"]` and `["-s", "--aihelp"]` writes the generator's output
      to the output writer, nothing to the error writer, and returns `SurlExitCode.Ok`; and an
      unknown topic writes what the ADR's decision 8 says and returns the code it names.
- [ ] `dotnet build Surl.Cli.UnitLibrary -warnaserror` and `dotnet build Surl.Console
      -warnaserror` are clean; `dotnet test --filter "TestCategory!=Integration"` passes;
      `Surl.Cli.UnitLibrary` and `Surl.Console` stay at 100% line and branch coverage.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
