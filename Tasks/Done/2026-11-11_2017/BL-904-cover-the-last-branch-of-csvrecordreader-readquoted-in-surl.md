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
completed: 2026-11-11
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

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Cli.UnitLibrary` reports 100% branch
      coverage for `Surl.Cli.UnitLibrary`.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-11-11: Measured first. `CsvRecordReader.ReadQuoted` already reports 100% (4/4 on line 283, all its
  branches covered by `MailboxFileReaderTests` cases at lines 39, 47 and 80), so no test was needed for it.
  The library's one remaining gap (99.92% branches) was `FaultReplyShapes.IsWebSocketCloseCodeAllowedOnTheWire`
  at 90%, which BL-903 added: its `>= 1000` lower bound could never be false, because the caller
  `IsWebSocketClose` has already bounded the code to 1000 to 4999 through `IsCodeWithOptionalText`. Following
  this task's rule ("simplify the code so it no longer exists rather than excluding it"), the pattern is now
  `code is <= 1003 or (>= 1007 and <= 1014) or >= 3000`. Behaviour does not change: the existing
  `CommandLineParserFaultTests` rows (1000, 1003, 1004, 1005, 1007, 1014, 1015, 1016, 2999, 3000, 4999, 5000)
  still pass. The library now measures 100% lines and 100% branches, 0 failing members.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Surl.Cli.UnitLibrary measures 100% lines and branches again
