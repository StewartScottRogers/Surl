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
completed: 2026-09-29
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

- [x] `CommandLineParserTests` pin every parsing case the ADR's decision 1 lists (at least
      `--aihelp`, `--aihelp mqtt`, `--aihelp=mqtt`, `--aihelp ""`, `--aihelp all`, an
      option before and after it, `--no-aihelp`, and `-h`/`-V`/`-M` before it), each with the
      outcome and subject or `CommandLineFailure` the ADR gives.
- [x] `HelpTextTests` pin the new `--help` short list, `--help all` and `--help surl` pages
      with the `--aihelp` line exactly as the ADR gives it, laid out by ADR-0034's 79-column
      rule, and `--help --aihelp` answers its option page.
- [x] `CommandLineRunnerTests` show that `RunAsync` with `["--aihelp"]`, `["--aihelp",
      "mqtt"]`, `["--aihelp", "all"]` and `["-s", "--aihelp"]` writes the generator's output
      to the output writer, nothing to the error writer, and returns `SurlExitCode.Ok`; and an
      unknown topic writes what the ADR's decision 8 says and returns the code it names.
- [x] `dotnet build Surl.Cli.UnitLibrary -warnaserror` and `dotnet build Surl.Console
      -warnaserror` are clean; `dotnet test --filter "TestCategory!=Integration"` passes;
      `Surl.Cli.UnitLibrary` and `Surl.Console` stay at 100% line and branch coverage.

## Notes

- Delivered directly against ADR-0046 decisions 1, 2, 8 and 10 (the ADR is the plan): the
  `aihelp` row (`CommandLineOptionKind.AiHelp`, no short name, `OptionArgumentType.OptionalTopic`
  = `optional topic` / `a topic or all; see surl --aihelp` from decision 5's vocabulary),
  `CommandLineOutcome.ShowAiHelp`, `CommandLineParseResult.ShowAiHelp`/`AiHelpTopic`, the parser
  reading it as `--help` reads its subject, and `CommandLineRunner.RunAsync` answering it
  through `WriteHelp`.
- Decision 8 needs no runner code: `AiHelpText.Answer` (BL-139) already returns the
  unknown-topic answer on stdout, so the runner tests pin exit 0 and its first lines.
- Decision 10: `ManualText`'s `SEE ALSO` names `surl --aihelp`, and the exit-code-0 row reads
  "help, an --aihelp answer, the manual or the version was written, or / surl was stopped by
  Ctrl+C or SIGTERM." (wrapped at 79 columns as the section's other rows).
- `Measure-CodeQuality.ps1`: `Surl.Cli.UnitLibrary` and `Surl.Console` 100% line and branch,
  0 failing members. Cli tests 639, Console tests 191, all fast tests green.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl --aihelp [topic] writes ADR-0046's Markdown and exits 0; --aihelp is listed in --help, --help all and --help surl; --manual's SEE ALSO names it
