---
id: BL-989
title: Add the --share, --share-access and --bucket options with their help, manual and aihelp text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-988]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-989 — Add the --share, --share-access and --bucket options with their help, manual and aihelp text

## Goal

`surl --share`, `--share-access` and `--bucket` are accepted on the command line, refused as ADR-0101 says, and
documented in `--help`, the manual and `--aihelp`. With BL-988's composition in place they configure real shares
and buckets end to end. This completes what BL-663 set out to do.

## Context

- Last of the three tasks BL-663 was split into (see BL-987); follow BL-883's shape for the mail options.
- Decisions: ADR-0101 decisions 4, 5 and 7 (option rows, parse-time refusals such as a duplicate or conflicting
  item, help category) and ADR-0102 decisions 2, 4 and 5.
- `--aihelp` completeness (root `CLAUDE.md`): each option needs its argument type and categories, a `ManualText`
  section and `AiHelpProse` paragraphs; an `AiHelpExamples` entry whose precondition creates the share folder
  (`AiHelpExamplePrecondition`), which `CommandLineRunnerAiHelpTests` in `Application/Surl.Console.UnitTests` must
  supply.
- Stand-in: none beyond BL-988's; this task is the command-line surface only.

## Acceptance criteria

- [ ] Named tests in `Application/Surl.Cli.UnitTests` parse each option and refuse each malformed, duplicate or
      conflicting item with the ADR's exit code and text.
- [ ] Each option passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName` and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
