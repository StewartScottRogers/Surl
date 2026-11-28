---
id: BL-2121
title: Deliver to an alias's remaining targets when one target names no owner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2121 — Deliver to an alias's remaining targets when one target names no owner

## Goal

Looking up an alias after one of its targets stopped naming an owner gives the same recipients whatever order the
targets are listed in.

## Context

Found while working BL-2116 on 2026-11-28, and reproduced with a scratch program. `MailDomainRouter.Expand`
(`Protocols/Mail/Surl.MailStore.UnitLibrary/MailDomainRouter.cs`) resolves targets through a lazy `Select` with
`FirstOrDefault`, so it stops at the first target that has a problem and never resolves the ones after it.

- Domain `example.com`, accounts `ann` and `bob`, alias `team@example.com` to `ann@example.com`, `bob@example.com`.
- After `RetireOwner("ann")`, `LookUpRecipient("<team@example.com>")` answers `NoSuchAccount`, so `bob` gets nothing.
- With the targets listed `bob`, `ann`, it answers `Deliverable` for `bob`.

By reading the code, an `AddOwner` that makes a target's local part ambiguous by case should do the same; check it.
`MailDomainRouter`'s comments and the project's `CLAUDE.md` say each target is resolved again, and ADR-0185 governs
owners added and retired at run time. Configuration validation uses the same method and must still report a problem.

Stand-in (root `CLAUDE.md`): a test that retires one member of a mailing alias mid-run expects the others to go on
receiving mail.

## Acceptance criteria

- [ ] A test in `Surl.MailStore.UnitTests` retires the first of two alias targets and finds the second still `Deliverable`.
- [ ] A test does the same with the retired target listed second, with the same result.
- [ ] A test shows what an alias whose every target names no owner answers.
- [ ] The existing refusals of a misconfigured alias or catch-all at construction still hold, shown by passing tests.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
