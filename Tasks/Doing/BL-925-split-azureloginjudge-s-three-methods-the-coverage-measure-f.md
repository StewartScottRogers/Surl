---
id: BL-925
title: Split AzureLoginJudge's three methods the coverage measure finds past complexity 10
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary/AzureLoginJudge.cs]
requirement: none
created: 2026-11-06
completed:
---
# BL-925 — Split AzureLoginJudge's three methods the coverage measure finds past complexity 10

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.AzureStorage.UnitLibrary` reports no failing member.

## Context

- Measured during BL-715 (2026-11-06), with the Azure library at 100% line and branch coverage: Cobertura's
  complexity puts `AzureLoginJudge.RefusalAnswer` at 16, `LogInAsync` at 14 and `GrantingPermissionsOf` at 12,
  over the threshold of 10 (CA1502 passes them, so the build is clean). BL-715 did not change the file.
- Extract private methods until each is at most 10; behaviour unchanged, the existing `AzureLoginJudgeTests`
  stay green. Never raise the threshold in `CodeMetricsConfig.txt`.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.AzureStorage.UnitLibrary`
      reports 0 failing members.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Filed by BL-715.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
