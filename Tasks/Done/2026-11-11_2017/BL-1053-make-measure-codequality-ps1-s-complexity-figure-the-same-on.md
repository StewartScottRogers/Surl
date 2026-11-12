---
id: BL-1053
title: Make Measure-CodeQuality.ps1's complexity figure the same on every run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Measure-CodeQuality.ps1, .claude/agents/coverage-auditor.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-1053 — Make Measure-CodeQuality.ps1's complexity figure the same on every run

## Goal

Two runs of `Measure-CodeQuality.ps1 -Library 'Surl.Cli*'` on the same commit report the same
failing members.

## Context

- Found by BL-652 on 2026-11-11. On one unchanged working tree, four consecutive runs reported
  four different sets of `complexity` failures in `Surl.Cli.UnitLibrary`, for example:
  - run A: `ConfiguredFaultReader.ReadScope` and `CommandLineParser.ActOnAcceptedArgument` at 12;
  - run B: `MailboxFileReader.ReadMailbox` 18, `ReadHeader` 16, `ReadRecords` 12.
- The figure is the Cobertura `complexity` attribute from the Microsoft code coverage
  collector (`--collect:Code Coverage;Format=cobertura`). `ReadHeader`'s real complexity is
  about 4; the 16 counts its lambdas' bodies and the compiler's cached-delegate null checks,
  which the collector attributes to the enclosing method on some runs and not on others.
- The build's `CA1502` gate (`CodeMetricsConfig.txt`, limit 10) measures the source and is stable;
  the build was clean throughout.
- Options to weigh: take complexity from `CA1502` (e.g. build once with a lower reporting
  threshold and read its diagnostics) instead of Cobertura; or ignore compiler-generated
  methods and delegate-cache branches when merging; or take the minimum across reports
  rather than the maximum. Thresholds in `CodeMetricsConfig.txt` are Stewart's and do not change.

## Acceptance criteria

- [x] Running `Measure-CodeQuality.ps1 -Library 'Surl.Cli*'` twice on one commit gives the same
      failing-member table both times.
- [x] The script's header comment says where the complexity figure comes from and why it is stable.
- [x] `coverage-auditor`'s instructions match the script.

## Notes

- Chose the first option: complexity now comes from `CA1502`. After the tests the script
  builds the production projects again with a probe `CodeMetricsConfig.txt` (`CA1502: 1`,
  `TreatWarningsAsErrors=false`): the whole solution without `-Library`, only the matching
  projects with it. It parses each `warning CA1502: '<name>' has a cyclomatic complexity of
  '<n>'` line with its file and declaration line. Why: it is the same rule that gates the
  build, it counts source, and it is deterministic. Taking the minimum across Cobertura
  reports would not help, because one run's reports all agreed with each other; the
  variation was between runs.
- Mapping: a Cobertura method takes the diagnostic in its source file with the same name
  whose line is the last one at or above the method's first covered line. No diagnostic
  means 1, because CA1502 at threshold 1 reports every method above 1. Lambdas, local
  functions and state machines take their owner's figure.
- The probe log is saved as `ca1502-probe.log` in the results directory, so `-SkipTestRun`
  reuses it. A probe build of `Surl.Cli.UnitLibrary` and its references takes about 2 s.
- Checked: `MailboxFileReader.ReadHeader` is now 5 (Cobertura had said 16), `ReadMailbox` 8 and
  `ReadQuoted` 7, matching the probe. Two full runs on one commit gave the same
  failing-member table: one member, `TlsSrpCombination.RefuseWithoutTlsListenUrl`, which
  fails on branch only. The library row's branch percentage still moved (99.89 vs 99.9),
  which is Cobertura branch-count noise. Filed as BL-1054.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Measure-CodeQuality.ps1 takes complexity from a CA1502 probe build, so two runs on one commit report the same failing members
