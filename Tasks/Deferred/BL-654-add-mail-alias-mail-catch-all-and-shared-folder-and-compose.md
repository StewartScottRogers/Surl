---
id: BL-654
title: Add --mail-alias, --mail-catch-all and --shared-folder and compose them into the mail store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-883, BL-646, BL-675, BL-677]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-654 — Add --mail-alias, --mail-catch-all and --shared-folder and compose them into the mail store

## Goal

`surl --mail-alias`, `--mail-catch-all` and `--shared-folder`, and the `quota=` and `folders=` keys of
`--mailbox`, configure aliases, catch-alls, shared folders, quotas and folders in the one mail store, with group
members expanded.

## Context

- Decisions: BL-641's ADR (names, grammar, refusals, bounds, which option widens access - a catch-all does, and is
  warned at start per ADR-0032 section 9) and BL-642's ADR decisions 1 and 4.
- Builds on: BL-880 to BL-883 (mail options and composition, split from BL-653), BL-648 (aliases, catch-alls in the store), BL-675 (quotas,
  folders, shared folders in the store), BL-677 (IMAP shows them), BL-646 (group expansion).
- Where: `Application/Surl.Cli.UnitLibrary` (option rows, BL-645 item reader) and
  `Application/Surl.Composition.UnitLibrary` (passing them to `MailboxStore`).
- Refusals the ADR names: an alias to an unknown mailbox, a catch-all for an unconfigured domain, a shared folder
  member naming no account, a quota past the store's bound.
- `--aihelp` completeness as in BL-653.
- From BL-648: aliases and catch-alls reach the store as `MailAlias` and `MailCatchAll` in a
  `MailDomainConfiguration` (constructor's and `LoadAsync`'s `mailDomains`). The store throws
  `MailRouteException` for ADR-0102 decision 1.3's three start refusals - its `RouteKind` (alias or catch-all),
  `Problem` (`TargetOutsideMailDomains`, `TargetNamesNoMailbox`, `IncludesItself`) and `Address` choose the
  option name and text; composition maps it to exit 2. `MailDomainConfiguration` throws `ArgumentException` for
  what the option parser must refuse first (a malformed address, an address outside every domain, a repeated
  address, two catch-alls for one domain).

## Acceptance criteria

- [ ] Named tests in `Application/Surl.Cli.UnitTests` parse each option and key and refuse each malformed or
      conflicting item with the ADR's exit code and text.
- [ ] Named tests in `Application/Surl.Composition.UnitTests` show aliases, catch-alls, shared folders (with an
      `@group` member expanded), quotas and folders reaching the composed store, and the catch-all warning
      written at start.
- [ ] Each option passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName` and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and both
      libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: depends-on BL-653 replaced by BL-883, the last task of its split (BL-653 deferred: timed out after 120 min; Stewart asked for the split 2026-11-05).
- 2026-11-06: Backlog -> Deferred. Split into BL-993, BL-994 and BL-995 (over the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06)
