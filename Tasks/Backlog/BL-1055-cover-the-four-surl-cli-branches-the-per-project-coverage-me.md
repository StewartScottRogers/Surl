---
id: BL-1055
title: Cover the four Surl.Cli branches the per-project coverage merge now shows
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1054]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-11
completed:
---
# BL-1055 — Cover the four Surl.Cli branches the per-project coverage merge now shows

## Goal

`Measure-CodeQuality.ps1 -Library 'Surl.Cli*'` reports `Surl.Cli.UnitLibrary` at 100% branch
with no failing member.

## Context

- BL-1054 stopped the collector merging every test project's Cobertura report (it dropped
  conditions on random lines). With per-project reports merged by the script, four Surl.Cli
  members show a branch gap on every run (2026-11-11):
  - `CsvRecordReader.ReadQuoted(StringBuilder)` - `Application/Surl.Cli.UnitLibrary/MailboxFileReader.cs:268`, 90%
  - `OptionArgumentReader.RefuseAuthorizedKeys(string, int, CommandLineAuthorizedKeys)` - `OptionArgumentReader.cs:553`, 91.67%
  - `CommandLineParser.RefuseCombination(CommandLineReading)` - `CommandLineParser.cs:758`, 95.45%
  - `TlsSrpCombination.RefuseWithoutTlsListenUrl(...)` - `TlsSrpCombination.cs:72`, 87.5%
- At least the last is the compiler's cached-delegate null check for a method group
  (`listenUrls.Any(UsesTls)`): the `??=` cache is null only on the first call in a process,
  so one of its two paths is hit at most once. Look at the Cobertura `<conditions>` of each
  line to tell such a check from a real untested branch; a lambda or a static readonly
  delegate field removes the check where a test cannot.

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1 -Library 'Surl.Cli*'` prints `| Surl.Cli.UnitLibrary | 100 | 100 |`
      and `0 failing member(s)`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-11: Created.
