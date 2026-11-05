---
id: BL-646
title: Add --group and expand account groups into the shared configuration
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-642, BL-645, BL-614]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Mcp.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-646 — Add --group and expand account groups into the shared configuration

## Goal

`surl --group <name>=<members>` names a set of accounts that the member lists of `--shared-folder`,
`--share-access` and `--topic-access` can use (as `@name` or the ADR's syntax), and `Surl.Composition` expands
each group to account names before any store sees it.

## Context

- Decisions: BL-641's ADR (option name, grammar, refusals, bounds, help category) and BL-642's ADR decision 4
  (members, nesting of groups, where groups are resolved; planning: expanded at composition).
- Where: the option row and parse into the configuration model in `Application/Surl.Cli.UnitLibrary`
  (`CommandLineOptions`, `SurlCommandLine`, BL-645's item reader); the expansion in
  `Application/Surl.Composition.UnitLibrary` (created by BL-609, start sequence moved by BL-614), as a type the
  later option tasks call (planning `AccountGroupExpansion`).
- Refusals (exit code and text from BL-641's ADR): a member naming no account, a group naming itself or forming a
  cycle, a group given twice.
- `--aihelp` completeness: root `CLAUDE.md` "`surl --aihelp`". Until a member-list option exists the example can
  only show `--group` being accepted; say so truthfully in `AiHelpProse`.

## Acceptance criteria

- [x] Named tests in `Application/Surl.Cli.UnitTests` parse `--group` (one, several, repeated name refused, bad item
      refused with the ADR's text) into the configuration model.
- [x] Named tests in `Application/Surl.Composition.UnitTests` expand groups to account names, refuse an unknown
      member and a cycle with the ADR's exit code and text.
- [x] `--group` passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues` and
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName`; `OptionHelp`, `ManualText`
      and `AiHelpProse` describe it.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and both
      libraries keep 100% line and branch coverage.

## Notes

- **Built.** `Surl.Cli`: `ConfiguredGroup`, `ConfiguredGroupReader` (the `--group` item shape over BL-645's
  reader; a member twice in one list refused, row 14), `ConfiguredMember.AsWritten`, the `--group` row (category
  `auth`, not negatable), `SurlCommandLine.Groups`, and in `CommandLineParser` a group name given twice refused
  after the whole line (row 14, `option --group: group <name> is given twice`, with the try line) and the 10001st
  `--group` refused (row 13). `Surl.Composition`: `AccountGroupExpansion` (Compose, `Expand` for the later
  member-list options, `WriteStartLines`), composed in `ComposeAuthentication` right after the accounts and
  `--user-file` are read, carried on `ComposedAuthentication`, its notes written last in `WriteStartUpLines`.
- **Tests.** `CommandLineParserGroupTests` (Cli), `AccountGroupExpansionTests`,
  `SurlServerCompositionTests.ComposeAsync_ARefusedGroup_FailsWithFailedInitBeforeAnyListenerBinds`,
  `ComposeAsync_AGroupOfAUserFileAccount_ExpandsAfterTheUserFileIsRead` and
  `ComposedServingTests.ServeAsync_GroupsAtVerbose_NoteEachGroupBeforeTheStatusLine` (Composition). The pinned help,
  manual and prose-line tests gained the new row, paragraph and four `surl: (2) option --group:` lines.
- **Decisions (sensible defaults, within ADR-0101/0102):**
  - `everyone` as a group name and `@everyone` as a member are refused at start with ADR-0102 4.2's `(2)` texts
    (no try line), like row 18, not while parsing: the texts carry the `(2)` form of start refusals, and the
    later options that accept `@everyone` decide it in the same place.
  - Start refusals are checked in the order everyone, unknown member, cycle, each at its first instance in
    command-line order; a cycle names the first group, in command-line order, whose expansion meets itself
    again. Group and account names compare ordinally, as ADR-0101 decision 5 and the account book do.
  - The `-v` note is `* Group <name>: <n> accounts`, with the `* ` prefix of the other verbose start notes, and
    keeps ADR-0102's literal "accounts" for every count.
  - `--group` occurrences are counted on the command line only; BL-644 (config files) will count them across
    files, through the same counter.
  - `ConfigurationItemReader.ReadAttribute` (BL-645) measured complexity 12 > 10; split into `ReadKnownAttribute`
    with no behaviour change, since this task owns `Surl.Cli.UnitLibrary`.
- **Touches widened.** Added `Application/Surl.Mcp.UnitLibrary`: `CommandLinePathMembersTests` fails on any new
  `SurlCommandLine` property until `CommandLinePathMembers` classifies it, so `Groups` joined
  `MembersWithoutPaths`. No task in Doing names that project.
- **Not done here.** The ADR edits (row 18's `@everyone` texts, the decisions above) need
  `Documentation/Planning/Decisions`, which BL-503 holds while in Doing; filed as BL-733.
- **Measured.** `Measure-CodeQuality.ps1`: `Surl.Cli.UnitLibrary` and `Surl.Composition.UnitLibrary` at 100% line
  and branch with no failing member; every fast test project green.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. surl --group <name>=<member>,... is parsed into SurlCommandLine.Groups and expanded to account names at start, refusing unknown members, cycles and everyone
