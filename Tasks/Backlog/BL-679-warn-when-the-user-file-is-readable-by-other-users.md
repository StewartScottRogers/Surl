---
id: BL-679
title: Warn when the --user-file is readable by other users
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-644, BL-613]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-059
created: 2026-11-04
completed:
---
# BL-679 — Warn when the --user-file is readable by other users

## Goal

On Linux and macOS, `surl --user-file <file>` writes `surl: warning: <file> is readable by other users and may hold
passwords` at every start when the file's Unix mode grants group or other read, as ADR-0101 decision 5 warns for
config files and `--mailbox-file`.

## Context

- Decision: ADR-0101 (BL-641) decision 5, "A file readable by other users": the exact text, start-up warning order
  and level (ADR-0032 section 9, ADR-0033), no warning on Windows.
- Builds on: BL-644, which writes the same warning for `-K` files and should leave a shared helper to call; BL-613,
  which moves `AuthenticationComposition` (where `--user-file` is read today, `Application/Surl.Console/
  AuthenticationComposition.cs`) into `Application/Surl.Composition.UnitLibrary`.
- Tests must stay platform-neutral: the mode check is behind a seam, and the Unix-only path is tested with
  `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` where it touches a real file.

## Acceptance criteria

- [ ] A named test in `Application/Surl.Composition.UnitTests` shows the warning written for a user file whose mode
      grants group or other read, and none for mode `0600`, through the mode seam.
- [ ] A named test shows no warning on Windows (the seam reports no mode).
- [ ] The `--user-file` `AiHelpProse` paragraph and `ManualText` mention the warning.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      touched libraries keep 100% line and branch coverage.

## Notes

- Filed by BL-641 (ADR-0101) as follow-up work; not widened into BL-644, whose `touches` do not include the
  composition.

## Log

- 2026-11-04: Created.
