---
id: BL-993
title: Read --mail-alias, --mail-catch-all and --shared-folder items and the --mailbox quota and folders keys into the Surl.Cli configuration model
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-883, BL-646, BL-1035]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Documentation/Planning/Decisions/ADR-0101-surls-configuration-surface-the-config-file-repeatable-item-options-and-one-configuration-model.md]
requirement: none
created: 2026-11-06
completed: 2026-11-11
---
# BL-993 — Read --mail-alias, --mail-catch-all and --shared-folder items and the --mailbox quota and folders keys into the Surl.Cli configuration model

## Goal

`Surl.Cli` can read a `--mail-alias` item, a `--mail-catch-all` item, a `--shared-folder` item and the `quota=` and
`folders=` keys of a `--mailbox` item into the configuration model, each read and refused as ADR-0101 says, fully
unit tested. No option row uses the new readers yet; BL-995 adds the rows.

## Context

- First of the three tasks BL-654 ("Add --mail-alias, --mail-catch-all and --shared-folder and compose them into
  the mail store") was split into on 2026-11-06 for the 60-minute size limit, in the order BL-653's split proved:
  this (model and readers), BL-994 (composition), BL-995 (option rows, help, manual and `--aihelp`).
- Decisions: ADR-0101 (BL-641: names, item grammar read by BL-645's `ConfigurationItemReader`, refusals, bounds)
  and ADR-0102 (BL-642) decisions 1 and 4.
- What the parser must refuse first, because `MailDomainConfiguration` throws `ArgumentException` for it (from
  BL-648): a malformed address, an address outside every domain, a repeated address, two catch-alls for one domain.
  Refusals that need the configured mailboxes (an alias to an unknown mailbox, a shared-folder member naming no
  account, a quota past the store's bound) are the ADR's; place each where the ADR puts it.
- `ConfiguredMailbox` and `ConfiguredMailboxReader` are BL-880's; the `quota=` and `folders=` keys extend them.
- Adding `SurlCommandLine` members makes `Surl.Mcp.UnitTests`'
  `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` fail until each is classified in
  `Application/Surl.Mcp.UnitLibrary` - a one-line entry per member.
- Stand-in: none in this task; it reads configuration only.

## Acceptance criteria

- [x] Named tests in `Application/Surl.Cli.UnitTests` read each item and key and refuse each malformed or
      conflicting item with the ADR's text, none echoing a password.
- [x] `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` passes.
- [x] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 2): `dotnet build` fails with "A compatible .NET SDK was not found" - `global.json` pins 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed. No gate can run, so the task waits on BL-1035 (Stewart reinstalls the SDK), as BL-766 and BL-990 do.
- 2026-11-11 (lane 1): SDK 10.0.404 is installed now and rolls forward from the pin; every gate ran.
- Delivered: `ConfiguredMailAlias`, `ConfiguredMailCatchAll`, `ConfiguredSharedFolder` and their readers over
  `ConfigurationItemReader`; `ConfiguredMailbox` gains `QuotaBytes` and `Folders` (a three-argument constructor
  keeps every existing caller, `Surl.Composition`'s included, unchanged); `ConfiguredMailboxReader.ReadWithSettings`
  reads `quota=` and `folders=`; `ConfiguredMailConflicts.Find` holds the whole-line duplicates and conflicts;
  `SurlCommandLine.MailAliases`, `MailCatchAlls`, `SharedFolders`, classified in `CommandLinePathMembers`.
- Choice: the `--mailbox` row and `MailboxFileReader` stay on the old shape, so `quota=` and `folders=` are still
  refused on the command line until BL-995 moves them (its Goal says that task makes them accepted) - otherwise
  surl would accept settings nothing composes before BL-994 and its help would not describe them.
- Choice: alias, catch-all and shared-folder records keep what was written, as ADR-0101 decision 4 says
  ("the records hold what was written, decoded"), so refusals quote it; comparisons normalise.
- Choice: where `MailDomainConfiguration` throws, the reader refuses first: a malformed address or domain, a
  folder or shared-folder name past 1024 UTF-8 bytes or not UTF-8, `INBOX` as a shared folder, a quota below 1,
  a repeated alias/catch-all/shared folder, an alias also a mailbox, an address or catch-all outside every
  domain. Targets resolving and `@everyone` are left to the store and composition, where ADR-0102 puts them.
  New texts recorded in ADR-0101 Amendment 1 (Decided by Claude under Stewart's delegation).
- Followers: notes added to BL-994 and BL-995 naming what each now does.
- touches gains ADR-0101 for Amendment 1 (the new refusal texts); no task in Doing names it.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so dotnet build fails
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Surl.Cli reads --mail-alias, --mail-catch-all, --shared-folder items and --mailbox quota=/folders= into the configuration model, refused as ADR-0101 says
