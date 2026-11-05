---
id: BL-645
title: Read the repeatable item-option argument grammar in Surl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-641]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-645 — Read the repeatable item-option argument grammar in Surl.Cli

## Goal

`Surl.Cli` reads the one argument grammar every layer-2 option shares (`<name>=<value>[;flag][;key=value]…` and
comma-separated member lists, as BL-641's ADR fixes it) into a typed item, with each malformed item refused in
the ADR's words, so the mail, file, message and group options are each a few lines.

## Context

- Decision: BL-641's ADR, decisions 3, 5 and 6 (grammar, quoting and escaping of `;`, `,`, `=`, `:`, unknown keys
  and flags, refusal texts, item bounds, and the `OptionArgumentType` reported to `--aihelp`).
- Where: `Application/Surl.Cli.UnitLibrary/OptionArgumentReader.cs`, `OptionArgumentReading.cs` and
  `OptionArgumentType.cs` - add the item reader as an `OptionArgumentReading<T>` so `WithArgument<T>` can take it
  (root `CLAUDE.md`, "`surl --aihelp`"). No option uses it yet; the option tasks (BL-646, BL-653, BL-654, BL-663,
  BL-666) do. Do not add any option here.
- A secret inside an item (a mailbox password) is never echoed in a refusal.

## Acceptance criteria

- [ ] Named tests read: a bare name; `name=value`; flags; `key=value` pairs; a comma list; each escape or quote
      form the ADR allows; and refuse with the ADR's exact text an empty name, a missing `=`, a repeated key, an
      unknown key or flag (as the ADR says), an item past the length bound, a list past the count bound, and a
      malformed item holding a secret without echoing it.
- [ ] The new `OptionArgumentType` member is covered by `AiHelpFactsTests` (its allowed-values text exists).
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
