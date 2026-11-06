---
id: BL-995
title: Add the --mail-alias, --mail-catch-all and --shared-folder options with their help, manual and aihelp text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-994]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-995 — Add the --mail-alias, --mail-catch-all and --shared-folder options with their help, manual and aihelp text

## Goal

`surl --mail-alias`, `--mail-catch-all` and `--shared-folder`, and the `quota=` and `folders=` keys of
`--mailbox`, are accepted on the command line, refused as ADR-0101 says, and documented in `--help`, the manual and
`--aihelp`. With BL-994's composition in place they configure the mail store end to end. This completes what
BL-654 set out to do.

## Context

- Last of the three tasks BL-654 was split into (see BL-993); follow BL-883's shape.
- Decisions: ADR-0101 decisions 4, 5 and 7 (option rows, parse-time refusals, help category `mail`) and ADR-0102
  decisions 1 and 4.
- `--aihelp` completeness (root `CLAUDE.md`): each option needs its argument type and categories, a `ManualText`
  section and `AiHelpProse` paragraphs under `mail`; any new `AiHelpExamples` entry is run by
  `CommandLineRunnerAiHelpTests` in `Application/Surl.Console.UnitTests`.
- Stand-in: none beyond BL-994's; this task is the command-line surface only.

## Acceptance criteria

- [ ] Named tests in `Application/Surl.Cli.UnitTests` parse each option and key and refuse each malformed or
      conflicting item with the ADR's exit code and text.
- [ ] Each option passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName` and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
