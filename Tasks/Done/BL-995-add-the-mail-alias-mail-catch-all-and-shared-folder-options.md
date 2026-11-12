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
completed: 2026-11-11
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

- [x] Named tests in `Application/Surl.Cli.UnitTests` parse each option and key and refuse each malformed or
      conflicting item with the ADR's exit code and text.
- [x] Each option passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName` and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (from BL-993): the readers are `ConfiguredMailAliasReader.Reading`, `ConfiguredMailCatchAllReader.Reading`
  and `ConfiguredSharedFolderReader.Reading`; the whole-line checks are `ConfiguredMailConflicts.Find` (it needs each
  item's option name as written, so keep them as `MailboxOptionNames` is kept). For `quota=` and `folders=`, move the
  `--mailbox` row (and `CommandLineParser.ReadMailbox`) from `ConfiguredMailboxReader.Reading`/`Shape` to
  `ReadingWithSettings`/`ShapeWithSettings`, then drop the old pair; and add `quota` and `folders` columns to
  `MailboxFileReader` (ADR-0101 decision 4: an empty field means the key was not given), passing them to `Build`.
  The refusal texts are ADR-0101 Amendment 1.

- 2026-11-11 (BL-995): done as the note says. `ConfiguredMailboxReader` now has one pair, `Shape` and `Read` (the settings pair renamed over the old one). `--mail-alias`, `--mail-catch-all` and `--shared-folder` argument names shortened to `<alias>=<to>,...`, `<domain>=<to>` and `<name>=<who>,...` so `--help all` stays within 79 columns. `ConfiguredMailboxReader.CheckFolders` now refuses an empty folder name, which a CSV `folders` field could otherwise carry in.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. --mail-alias, --mail-catch-all, --shared-folder and the mailbox quota= and folders= keys parse, refuse per ADR-0101, and are in help, manual and aihelp
