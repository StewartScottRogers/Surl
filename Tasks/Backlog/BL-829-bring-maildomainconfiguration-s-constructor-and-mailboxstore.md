---
id: BL-829
title: Bring MailDomainConfiguration's constructor and MailboxStore.DeliverPending to complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1035]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-829 — Bring MailDomainConfiguration's constructor and MailboxStore.DeliverPending to complexity 10

## Goal

`Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary` reports 0 failing members.

## Context

Found by BL-677 (2026-11-05): the Cobertura complexity of `MailDomainConfiguration..ctor` (MailDomainConfiguration.cs:34) and
`MailboxStore.DeliverPending` (MailboxStore.cs:402) is 12, past the threshold of 10; the build's CA1502 gate passes them, so
only the measurement shows it. Both came in with BL-648/BL-675. Extract private methods; behaviour unchanged.

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary` reports 0 failing members, with 100% line and branch.
- [ ] Every existing test in `Protocols/Mail/Surl.MailStore.UnitTests` passes unchanged; build clean, fast tests green.

## Notes

- 2026-11-11 (lane 6): `Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary` cannot run: `dotnet test` fails with "A compatible .NET SDK was not found" (global.json pins 10.0.401; only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed). Returned to Backlog behind BL-1035. Plan when it resumes: in the constructor, move the four `ThrowIfNull` calls and the `?? []` defaults into private methods (e.g. `NormaliseMailboxSettings`, `NormaliseSharedFolders`); in `DeliverPending`, move the quota check (with its `GroupBy` lambda) into a private `IsAnyOwnerOverQuota`, keeping the `lock` body flat.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so build and Measure-CodeQuality.ps1 cannot run
