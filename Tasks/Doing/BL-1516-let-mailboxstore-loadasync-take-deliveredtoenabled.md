---
id: BL-1516
title: Let MailboxStore.LoadAsync take DeliveredToEnabled
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1507]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1516 — Let MailboxStore.LoadAsync take DeliveredToEnabled

## Goal

`MailboxStore.LoadAsync` takes an optional `deliveredToEnabled` argument (default false) and the loaded store reports it as `DeliveredToEnabled`.

## Context

`DeliveredToEnabled` is init-only, so a store built by `LoadAsync` (the file-backed path) cannot have it set by the composition root (BL-1502); the constructor path can use an object initializer. Add the parameter after `faults` and set the property in `LoadAsync`. Split from BL-1502, which needs it.

## Acceptance criteria

- [ ] A `MailboxStore` test: `LoadAsync` with `deliveredToEnabled: true` gives a store whose `DeliveredToEnabled` is true; omitted, false.
- [ ] `dotnet build` is clean and the MailStore fast tests pass at 100% line and branch coverage.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
