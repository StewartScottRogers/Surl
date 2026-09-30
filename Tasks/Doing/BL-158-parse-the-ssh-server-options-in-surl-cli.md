---
id: BL-158
title: Parse the SSH server options in Surl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-154]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests, Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs, Surl.Protocol.Abstractions.UnitTests/SurlExitCodeTests.cs]
requirement: FR-039
created: 2026-09-29
completed:
---
# BL-158 — Parse the SSH server options in Surl.Cli

## Goal

`surl` parses the SSH server options BL-154's ADR decides (host key and authorized keys, or
whatever the ADR names) into `SurlCommandLine`, with their help, AI-help facts and manual
text, and refuses a start that gives them until BL-171 composes the SSH server.

## Context

- Decision: BL-154's ADR (option names, arguments, defaults, descriptions, categories, refusal
  texts, any new `SurlExitCode` member).
- Code: `Surl.Cli.UnitLibrary/CommandLineOptions.cs` (one row per option with its
  `OptionHelp`; see `--user-file` for a `<file>` option), `SurlCommandLine.cs`,
  `OptionArgumentType.cs`, `ManualText.cs`, `AiHelpProse.cs`; `Surl.Cli.UnitLibrary/CLAUDE.md`
  (an option cannot be added without its help; a changed behaviour updates `ManualText` and
  `AiHelpProse` in the same change). `AiHelpFactsTests` fail when an option or a
  `SurlExitCode` member is missing from the AI help.
- Categories: the ADR's non-protocol categories only; the SSH protocol category joins in BL-171
  (ADR-0034 decision 1: the registering task adds it).
- Until BL-171, the options would describe listeners that do not exist. Follow ADR-0032
  section 1's precedent for `--auth` words: `Surl.Console` refuses a start that gives one with
  `FailedInit` (2), `surl: (2) --<option> is not available in this build`, no `try` line;
  BL-171 removes the refusal. This is why `Surl.Console` is in `touches`.
- A new `SurlExitCode` member, if the ADR adds one, goes into
  `Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs` with its test in `SurlExitCodeTests`
  and its `ExitCodeGuidanceTable` row (ADR-0046) in this same change.

## Acceptance criteria

- [ ] Each option BL-154's ADR names parses as it says (argument kind, default, repeats,
      negation), with a `CommandLineParserTests` case for each refusal text the ADR gives.
- [ ] `surl --help <option>` and `--help all` show the ADR's description, in its categories;
      `ManualText` and `AiHelpProse` describe the options; `AiHelpFactsTests`,
      `AiHelpTextTests`, `HelpTextTests` and `ManualTextTests` pass.
- [ ] A `Surl.Console.UnitTests` test shows a start giving one of the options returns
      `FailedInit` with the text in Context.
- [ ] Any new `SurlExitCode` member has its number, its `SurlExitCodeTests` case and its
      `ExitCodeGuidanceTable` row.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green;
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member in
      `Surl.Cli.UnitLibrary` and `Surl.Console`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
