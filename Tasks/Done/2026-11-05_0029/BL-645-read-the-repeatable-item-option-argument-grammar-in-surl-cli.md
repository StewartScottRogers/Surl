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
completed: 2026-11-04
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

- [x] Named tests read: a bare name; `name=value`; flags; `key=value` pairs; a comma list; each escape or quote
      form the ADR allows; and refuse with the ADR's exact text an empty name, a missing `=`, a repeated key, an
      unknown key or flag (as the ADR says), an item past the length bound, a list past the count bound, and a
      malformed item holding a secret without echoing it.
- [x] The new `OptionArgumentType` member is covered by `AiHelpFactsTests` (its allowed-values text exists).
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Built `ConfigurationItemReader`, `ConfigurationItemShape`, `ConfigurationItem`,
  `ConfigurationItemValueKind`, `ConfigurationItemAttributeKind`, `ConfiguredMember` and
  `ConfiguredGrant` in `Surl.Cli.UnitLibrary`, plus `OptionArgumentType.Item` and `ItemSyntax`.
  Tests: `ConfigurationItemReaderTests` and
  `AiHelpFactsTests.ItemArgumentType_IsNamedByItsShapeAndEndsWithTheSharedSentence`; new code 100%
  line and branch.
- Defaults taken where ADR-0101 is silent (all within its decision 3 and row 10 texts):
  - An empty field after the separator (`ann:`, an empty password or path) is refused as
    `an empty field`, as `--user` refuses an empty password.
  - A key given twice and a flag given twice both answer `key <k> is given twice`; keys are
    compared after lower-casing.
  - Member (`@group`) and grant (`<member>:<access>`, split at the last `:` before decoding)
    lists are read here, not by each option, because both splits must happen before
    percent-decoding; an access word outside the option's own is `is badly used here`, and a
    grant without `:` is `expected <shape>`.
  - `--topic-map`'s two-sided head is left to BL-666: read it with a `Name` value kind and split
    each side itself, or add a value kind then.
  - Row 13's count bound needs the parser that sees every occurrence (BL-644 and the option
    tasks); this task supplies `MaxOccurrences` and `GivenTooOften` only.
  - A duplicate member in one list (row 14) is left to BL-646 with the other duplicate checks.
- Literal (unescaped) text is taken as written; only bytes from `%` escapes are checked for UTF-8.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Cli reads the layer-2 item grammar (ADR-0101 decision 3, rows 10-12) into a ConfigurationItem via ConfigurationItemShape.Reading
