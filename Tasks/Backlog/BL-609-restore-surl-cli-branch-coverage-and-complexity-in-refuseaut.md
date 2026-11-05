---
id: BL-609
title: Restore Surl.Cli branch coverage and complexity in RefuseAuthorizedKeys and CategoryPageLines
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-609 — Restore Surl.Cli branch coverage and complexity in RefuseAuthorizedKeys and CategoryPageLines

## Goal

`Measure-CodeQuality.ps1 -Library 'Surl.Cli*'` reports 0 failing members for `Surl.Cli.UnitLibrary`.

## Context

- Found by BL-370's coverage run on 2026-11-04 (after BL-575, commit `2efd44f9`, touched both files):
  - `OptionArgumentReader.RefuseAuthorizedKeys(string, int, CommandLineAuthorizedKeys)`
    (`Application/Surl.Cli.UnitLibrary/OptionArgumentReader.cs:393`): branch 91.67%, cyclomatic
    complexity 12 (limit 10).
  - `HelpText.CategoryPageLines(HelpCategory)` (`Application/Surl.Cli.UnitLibrary/HelpText.cs:120`):
    branch 75%.
- Thresholds live in `CodeMetricsConfig.txt` and are Stewart's; split the method rather than
  raising one.

## Acceptance criteria

- [ ] `RefuseAuthorizedKeys` has cyclomatic complexity of at most 10 and 100% branch coverage.
- [ ] `CategoryPageLines` has 100% branch coverage.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
