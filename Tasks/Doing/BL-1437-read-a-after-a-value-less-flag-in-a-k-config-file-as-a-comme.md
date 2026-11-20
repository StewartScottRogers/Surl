---
id: BL-1437
title: Read a # after a value-less flag in a -K config file as a comment
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1437 — Read a # after a value-less flag in a -K config file as a comment

## Goal

A `-K` config file line `allow-uploads  # note` (or `--allow-uploads # note`, `-v # note`) is read as
the flag followed by a comment, as `directory site  # note` already is, instead of exiting 2 with
`text follows the value of --allow-uploads`.

## Context

- Surly bug report `2026-11-19T1942-Surl-bug-report.md`, item 2 (Surl `763754785`, unchanged since):
  `fx.conf:3: text follows the value of --allow-uploads; quote a value that holds a space`, exit 2.
- ADR-0101 decision 1 rows 3, 7 and 8 allow a `#` comment after spaces or tabs following a value. They
  say nothing of a flag, which has no value, so this applies the same rule; no decision changes.
- Cause: `Application/Surl.Cli.UnitLibrary/ConfigFileText.cs:93-110` (`ReadOption`). After the name,
  `ReadUndashedName`/`ReadDashedName` skip the blanks and `valueStart` lands on `#`. `ReadOption` then
  takes `#` as the start of an unquoted value (`ReadUnquotedValue`, `:113-119`), and `note` after it is
  refused.
- The fix applies only to an option that takes no value (`CommandLineOptionKind.Flag`, looked up as
  `NameWithoutValue` does through `CommandLineOptions`). A valued option keeps reading a value that
  starts with `#` (`header #x`, `url #frag`), as upstream curl and today's Surl do. A `#` straight after
  `=` or `:` with no blank (`allow-uploads=#`) is not a comment.

## Acceptance criteria

- [ ] `allow-uploads  # note`, `--allow-uploads # note`, `-v # note` and `allow-uploads\t#` in a config
      file each set the flag and nothing else (DataRow cases in
      `Application/Surl.Cli.UnitTests/CommandLineParserConfigFileTests.cs`).
- [ ] `header #x` still reads the value `#x`, and `directory site  # note` still reads `site` (existing
      or new DataRows).
- [ ] The existing refusals in `CommandLineParserConfigFileTests` (`-m30 b`, `--log-file=a.log b.log`,
      `user = "ann:secret" more`) still exit 2 with their texts.
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests are green,
      and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T1942-Surl-bug-report.md` item 2.
- 2026-11-20: Backlog -> Doing.
