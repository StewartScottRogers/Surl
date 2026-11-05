---
id: BL-825
title: Bring MailDomainConfiguration's constructor and MailboxStore.DeliverPending to complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-825 — Bring MailDomainConfiguration's constructor and MailboxStore.DeliverPending to complexity 10

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

## Log

- 2026-11-05: Created.
