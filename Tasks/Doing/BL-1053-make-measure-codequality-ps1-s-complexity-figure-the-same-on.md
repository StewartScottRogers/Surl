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
completed:
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

- [ ] Running `Measure-CodeQuality.ps1 -Library 'Surl.Cli*'` twice on one commit gives the same
      failing-member table both times.
- [ ] The script's header comment says where the complexity figure comes from and why it is stable.
- [ ] `coverage-auditor`'s instructions match the script.

## Notes

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
