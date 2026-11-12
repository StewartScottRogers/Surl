---
id: BL-992
title: Add the --topic, --queue, --topic-map and --topic-access options with their help, manual and aihelp text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-991]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-11
---
# BL-992 — Add the --topic, --queue, --topic-map and --topic-access options with their help, manual and aihelp text

## Goal

`surl --topic`, `--queue`, `--topic-map` and `--topic-access` are accepted on the command line, refused as
ADR-0101 says, and documented in `--help`, the manual and `--aihelp`. With BL-991's composition in place they
configure the real bus end to end. This completes what BL-666 set out to do.

## Context

- Last of the three tasks BL-666 was split into (see BL-990); follow BL-883's shape for the mail options.
- Decisions: ADR-0101 decisions 4, 5 and 7 (option rows, parse-time refusals, help category) and ADR-0102
  decisions 3, 4 and 5.
- `--aihelp` completeness (root `CLAUDE.md`): each option needs its argument type and categories, a `ManualText`
  section and `AiHelpProse` paragraphs; an `AiHelpExamples` entry, run by `CommandLineRunnerAiHelpTests` in
  `Application/Surl.Console.UnitTests`.
- Stand-in: none beyond BL-991's; this task is the command-line surface only.

## Acceptance criteria

- [x] Named tests in `Application/Surl.Cli.UnitTests` parse each option and refuse each malformed, duplicate or
      conflicting item with the ADR's exit code and text.
- [x] Each option passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName` and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Followed BL-989's shape. Rows use category `messages` only (ADR-0101 decision 7); the new `messages` help category, its `AiHelpProse` topic, `ManualText` section "TOPICS AND QUEUES" and one MQTT `--topic` example are added here. Decided without an ADR: ADR-0101/0102 already fix every behaviour.
- Row argument names shortened (`<right>`, "Alias a topic") to keep `--help all` lines within 79 columns.
- Verified: `dotnet build -warnaserror` clean; Surl.Cli.UnitTests 1698 and Surl.Console.UnitTests 464 pass.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. --topic, --queue, --topic-map and --topic-access parse, refuse as ADR-0101 says, and appear in --help, the manual and --aihelp (messages topic)
