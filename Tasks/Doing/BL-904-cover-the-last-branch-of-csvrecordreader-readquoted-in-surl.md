---
id: BL-904
title: Cover the last branch of CsvRecordReader.ReadQuoted in Surl.Cli
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-904 — Cover the last branch of CsvRecordReader.ReadQuoted in Surl.Cli

## Goal

`Surl.Cli.UnitLibrary` is back at 100% branch coverage: the one uncovered branch of `CsvRecordReader.ReadQuoted`
(`Application/Surl.Cli.UnitLibrary/MailboxFileReader.cs:268`) is exercised by a `MailboxFileReaderTests` case.

## Context

- Found by BL-815's coverage run on 2026-11-05: `Measure-CodeQuality.ps1` reports `Surl.Cli.UnitLibrary` at 100%
  lines and 99.89% branches, the gap being `ReadQuoted` at 93.75% (one branch). It came in with commit 775a4456
  (mailbox CSV files), not with BL-815.
- If the branch cannot be reached, simplify the code so it no longer exists rather than excluding it.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Cli.UnitLibrary` reports 100% branch
      coverage for `Surl.Cli.UnitLibrary`.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
