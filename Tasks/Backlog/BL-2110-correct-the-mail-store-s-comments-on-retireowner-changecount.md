---
id: BL-2110
title: Correct the mail store's comments on RetireOwner, ChangeCount and when files are written
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary]
requirement: none
created: 2026-11-28
completed:
---
# BL-2110 — Correct the mail store's comments on RetireOwner, ChangeCount and when files are written

## Goal

The XML comments of `Surl.MailStore.UnitLibrary` say what `RetireOwner` leaves reachable, which operations raise
`ChangeCount` and when the store writes its files. No behaviour changes.

## Context

Found while working BL-2108 on 2026-11-28. Check each finding against the code before changing a statement. All paths
are under `Protocols/Mail/Surl.MailStore.UnitLibrary/`.

1. The `<summary>` of `RetireOwner` (`MailboxStore.Control.cs`) says `ViewFor` then gives an empty view. `ViewFor`
   appears to match shared-folder membership by name, so a retired account that is a member would still see those
   folders.
2. The same summary says `LookUpRecipient` answers `NoSuchAccount`. With mail domains, a domain catch-all would make
   the recipient deliverable, and so might another account whose name differs only in case.
3. The `<returns>` or summary of `AddOwner` says "A store on disk counts the new owner as a change". Every store raises
   `ChangeCount`; only a store on disk writes it.
4. The `<remarks>` of `MailboxStore` and the summary of `ChangeCount` say every change is counted. `RetireOwner` and an
   `AddOwner` that reaches a retired owner again change who is reached and count nothing.
5. The `files` parameter of `LoadAsync` says the files are "written to after each change". The store writes only when
   `SaveChangesAsync` is called.

Stand-in (root `CLAUDE.md`, "Surl is a stand-in for tests"): a test that retires an account mid-run, or waits on
`ChangeCount`, relies on these statements. The task adds no store, event, seed, reset or fault behaviour. A finding
that turns out to be a behaviour defect is filed as its own task.

## Acceptance criteria

- [ ] The summary of `RetireOwner` states what `ViewFor` gives a retired shared-folder member and what `LookUpRecipient` answers when a catch-all or another account matches, as the code does.
- [ ] No comment on `AddOwner`, `ChangeCount` or the `MailboxStore` class says or implies that `RetireOwner` or reaching a retired owner raises `ChangeCount`, or that only a store on disk counts a new owner.
- [ ] The `files` parameter comment of `LoadAsync` says the files are written by `SaveChangesAsync`.
- [ ] `dotnet build Protocols/Mail/Surl.MailStore.UnitLibrary -warnaserror` is clean, and the diff changes only comments and `CLAUDE.md` in that project (and this task's own file).

## Notes

## Log

- 2026-11-28: Created.
