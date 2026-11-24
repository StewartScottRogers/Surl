---
id: BL-1637
title: Cover MailboxStore.ApplyMailboxSettings' folder-use branch
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-1637 — Cover MailboxStore.ApplyMailboxSettings' folder-use branch

## Goal

`Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Surl.MailStore.UnitLibrary 100%
  line, 99.87% branch, 1 failing member.
- `MailboxStore.ApplyMailboxSettings(IEnumerable<MailboxSettings>)`,
  `Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.cs:1569`, 90% branch. Line 1579
  (`var uses = mailbox.FolderUses ?? new Dictionary<string, string>();`) is 1 of 2: `MailStore.UnitTests` only builds a
  store from `MailboxSettings` whose `FolderUses` is always null or always set, never both.
- `FolderUses` is the SPECIAL-USE configuration from BL-1510 (commit a384a7df6; ADR-0055 Amendment 1).

## Acceptance criteria

- [ ] A named `Surl.MailStore.UnitTests` test builds a store from `MailboxSettings` with `FolderUses` null, and one with
      `FolderUses` naming a folder that is not in `Folders` (for example `Archive` → `\Archive`). It asserts each
      folder is created, with the configured use for the second (the uses keys are created as folders too, line 1580).
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary` reports `100 | 100` with
      0 failing members, and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Tests only (test-writer).

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
