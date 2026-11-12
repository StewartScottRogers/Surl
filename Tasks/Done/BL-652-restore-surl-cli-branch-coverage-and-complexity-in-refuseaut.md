---
id: BL-652
title: Restore Surl.Cli branch coverage and complexity in RefuseAuthorizedKeys and CategoryPageLines
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-652 — Restore Surl.Cli branch coverage and complexity in RefuseAuthorizedKeys and CategoryPageLines

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

- [x] `RefuseAuthorizedKeys` has cyclomatic complexity of at most 10 and 100% branch coverage.
- [x] `CategoryPageLines` has 100% branch coverage.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-11-11 (lane 1): both named members already passed when measured: `RefuseAuthorizedKeys`
  (now `OptionArgumentReader.cs:552`) Cx 8, branch 100%; `CategoryPageLines` Cx 4, branch 100%.
  Later work had split and covered them.
- The Goal's "0 failing members" turned out to be unmeasurable: the Cobertura `complexity` attribute
  changes between runs of the same code (four runs, four different failing sets), because the
  collector sometimes folds lambda bodies and cached-delegate checks into the enclosing method.
  Filed as BL-1053. The `CA1502` build gate is stable and clean.
- Fixed what the runs did report, all in `Surl.Cli.UnitLibrary`:
  - `ConfigurationItemReader.ReadHeadValue` branch 75%: two call sites shared the compiler's lazily
    cached `ReadName` delegate, so which arm of its null check ran depended on test order. Now one
    `static readonly ReadNameMember` delegate, no branch.
  - `FaultReplyShapes.IsCoapResponseCode` (Cx 16): class/detail check split into `IsCoapErrorClassDetail`.
  - `CsvRecordReader.ReadQuoted` (Cx 12): closing-quote decision split into `EndQuotedField`.
  - `OptionArgumentReader.ReadProxyAllowedOrigin` (Cx 12): `address[/prefix]` part split into
    `ReadProxyAllowedOriginRange`.
  - `ConfiguredFaultReader.ReadReply` (Cx 12): scheme lookup split into `ReplySchemeOf`.
  - `TlsAuthenticationType.cs` had LF line endings that `dotnet format` refused; now CRLF.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. RefuseAuthorizedKeys and CategoryPageLines measure Cx 8/4 at 100% branch; five other Surl.Cli members split or fixed; unstable Cobertura complexity filed as BL-1053
