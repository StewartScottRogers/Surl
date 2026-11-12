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
completed:
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

- [ ] Two runs of `Measure-CodeQuality.ps1 -Library 'Surl.Cli*'` on one commit print the same
      `Surl.Cli.UnitLibrary` row, or the script's header comment names the cause and why it
      cannot be removed.

## Notes

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
