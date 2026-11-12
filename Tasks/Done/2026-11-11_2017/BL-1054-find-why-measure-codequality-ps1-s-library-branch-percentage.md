---
id: BL-1054
title: Find why Measure-CodeQuality.ps1's library branch percentage moves between runs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-1054 — Find why Measure-CodeQuality.ps1's library branch percentage moves between runs

## Goal

Two runs of `Measure-CodeQuality.ps1 -Library 'Surl.Cli*'` on the same commit report the same
library line and branch percentages, or the script's header says why they cannot.

## Context

- Found by BL-1053 on 2026-11-11. After BL-1053 took complexity from CA1502, two full runs on
  one commit gave the same failing-member table, but the `Surl.Cli.UnitLibrary` row's branch
  percentage was 99.89 on one run and 99.9 on the other: the Cobertura collector's branch
  count (`condition-coverage`) for some method differs between runs.
- Start by diffing two runs' `*.cobertura.xml` for `Surl.Cli.UnitLibrary` method by method
  (`condition-coverage` per line), and look for compiler-generated methods (lambdas,
  `<>c`, state machines) whose conditions appear in one run only.

## Acceptance criteria

- [x] Two runs of `Measure-CodeQuality.ps1 -Library 'Surl.Cli*'` on one commit print the same
      `Surl.Cli.UnitLibrary` row, or the script's header comment names the cause and why it
      cannot be removed.

## Notes

- Cause: not compiler-generated methods. `dotnet test Surl.slnx --collect` post-processes
  (merges) every test project's report into one Cobertura file. That merge
  (`Microsoft.CodeCoverage.Core`, fast/slow merge, `TryMergeConditions`) leaves some lines
  `branch="True"` with no `condition-coverage` at all, and which lines changes run to run:
  Surl.Cli.UnitLibrary had 238 such lines in one full run and 188 in the next (2,157 and
  2,581 across all production packages). `TlsSrpCombination.RefuseWithoutTlsListenUrl`
  line 72 read 7/8 in one run and had no conditions in the other: 99.89% vs 100%.
- A single test project's report has no condition-less branch line and was identical on
  two runs (Surl.Cli.UnitTests alone: line 72 is 7/8 every time).
- Fix: the script sets `VSTEST_DISABLE_ARTIFACTS_POSTPROCESSING=1` for its test run (and
  restores the old value), so each of the 80 test projects keeps its own report and the
  script's existing line-by-line max merge combines them. Chosen over running each test
  project separately because it keeps the one parallel `dotnet test` and its run time
  (about 2 minutes either way). Two end-to-end runs of the edited script both printed
  `| Surl.Cli.UnitLibrary | 100 | 99.68 | 542 | 4 | 8 |` with 0 condition-less lines.
- The truthful figure is lower: four Surl.Cli members have real branch gaps the merge had
  been hiding. Filed as BL-1055. Other libraries' published numbers may drop the same way
  on the next full measurement; that is the measurement becoming honest, not a regression.
- Verification: the script's own runs build the solution and ran the fast tests green four
  times; only `Measure-CodeQuality.ps1` changed (no `.cs`), so there is nothing to format.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Measure-CodeQuality.ps1 keeps one Cobertura report per test project, so two runs print the same library row
