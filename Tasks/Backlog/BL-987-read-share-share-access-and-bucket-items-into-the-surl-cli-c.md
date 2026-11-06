---
id: BL-987
title: Read --share, --share-access and --bucket items into the Surl.Cli configuration model
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-645, BL-646]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-987 — Read --share, --share-access and --bucket items into the Surl.Cli configuration model

## Goal

`Surl.Cli` can read a `--share <name>=<path>[;readonly;protocols=…]` item, a
`--share-access <name>=<user|@group>:<rw|r>,...` item and a `--bucket <name>=<path>` item into the configuration
model (`SurlCommandLine` members, each empty by default), each read and refused as ADR-0101 says, fully unit
tested. No option row uses the readers yet; BL-989 adds the rows.

## Context

- First of the three tasks BL-663 ("Add --share, --share-access and --bucket and compose them into the content
  store") was split into on 2026-11-06 for the 60-minute size limit, in the order BL-653's split proved (BL-880,
  BL-882, BL-883): this (model and readers), BL-988 (composition), BL-989 (option rows, help, manual and `--aihelp`),
  so every help sentence describes behaviour that already exists.
- Decisions: ADR-0101 (BL-641: names, item grammar read by BL-645's `ConfigurationItemReader`, refusals, bounds,
  a refusal never echoing a secret) and ADR-0102 (BL-642) decisions 2, 4 and 5. The ADRs win over this summary.
- ADR-0102 decision 2.1: a scheme and its TLS twin are one entry, so `protocols=gopher` also admits `gophers` and
  `ftp` admits `ftps` (`Account.AreTlsTwinsOrEqual` already does this for accounts; `ContentShare.IsVisibleTo`
  matches `Schemes` exactly, so the model or BL-988 must expand the twins - say which in the XML docs).
- Adding `SurlCommandLine` members makes `Surl.Mcp.UnitTests`'
  `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` fail until each is classified in
  `Application/Surl.Mcp.UnitLibrary` - a one-line entry per member.
- Stand-in: none in this task; it reads configuration only.

## Acceptance criteria

- [ ] Named tests in `Application/Surl.Cli.UnitTests` read each item form (one, many, `readonly`, `protocols=`,
      `@group` access) and refuse each malformed or duplicate item with the ADR's text.
- [ ] `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` passes.
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
