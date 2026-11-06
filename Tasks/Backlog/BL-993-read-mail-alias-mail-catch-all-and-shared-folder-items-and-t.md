---
id: BL-993
title: Read --mail-alias, --mail-catch-all and --shared-folder items and the --mailbox quota and folders keys into the Surl.Cli configuration model
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-883, BL-646]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-06
completed:
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

- [ ] Named tests in `Application/Surl.Cli.UnitTests` read each item and key and refuse each malformed or
      conflicting item with the ADR's text, none echoing a password.
- [ ] `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` passes.
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
