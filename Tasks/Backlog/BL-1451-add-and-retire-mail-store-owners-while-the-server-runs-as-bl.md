---
id: BL-1451
title: Add and retire mail-store owners while the server runs, as BL-1450 decides
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1450]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1451 — Add and retire mail-store owners while the server runs, as BL-1450 decides

## Goal

`MailboxStore` has the members BL-1450's ADR names to add an owner while the server runs (with the
mailboxes a start-time account gets) and to retire one, so `ViewFor`, `LookUpRecipient`, `Seed`,
`Reset`, `TakeSnapshot` and `Restore` all see the change.

## Context

- Surly bug report `2026-11-19T2203-Surl-bug-report.md` items 1 and 2.
- `Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.cs:132-150` (constructor, `ReachOwners`,
  `configuredMailboxes`), `ViewFor` (`:372`), `LookUpRecipient` (`:323`), and `MailboxStore.Control.cs`
  (seed, reset, snapshot, restore). Mail domains route through `MailDomainRouter` (account by local
  part).
- Every change publishes one `StoreChangedEvent` (`StoreKind.Mail`) under the store's lock, named by
  `MailStoreChangeName`, as the ADR says for these two operations. A refused or no-op call publishes
  nothing.
- The store never touches the disk directly; with files (`LoadAsync`) a new owner reaches the index on
  the next `SaveChangesAsync`.

## Acceptance criteria

- [ ] The members BL-1450 names exist, with XML doc comments. Fast tests in
      `Protocols/Mail/Surl.MailStore.UnitTests` show an added owner's `INBOX` selectable through
      `ViewFor` and deliverable through `LookUpRecipient`/`Deliver` and `Seed`.
- [ ] A retired owner is `NoSuchAccount` to `LookUpRecipient` and `Seed`, and its mail is kept or
      dropped as the ADR says (named tests).
- [ ] `Reset`, `TakeSnapshot` and `Restore` treat run-time owners as the ADR says (one named test each).
- [ ] Adding an owner that exists, retiring one that does not, the anonymous store, and a store at
      `MaxMailboxes` each give the ADR's result and publish nothing (named tests).
- [ ] `dotnet build Protocols/Mail/Surl.MailStore.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T2203-Surl-bug-report.md` items 1 and 2.
