---
id: BL-646
title: Add --group and expand account groups into the shared configuration
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-642, BL-645, BL-614]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-04
completed:
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

- [ ] Named tests in `Application/Surl.Cli.UnitTests` parse `--group` (one, several, repeated name refused, bad item
      refused with the ADR's text) into the configuration model.
- [ ] Named tests in `Application/Surl.Composition.UnitTests` expand groups to account names, refuse an unknown
      member and a cycle with the ADR's exit code and text.
- [ ] `--group` passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues` and
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName`; `OptionHelp`, `ManualText`
      and `AiHelpProse` describe it.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and both
      libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
