---
id: BL-2131
title: Read the mail store's accounts under its lock in LookUpRecipient and ViewFor
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2131 — Read the mail store's accounts under its lock in LookUpRecipient and ViewFor

## Goal

`MailboxStore.LookUpRecipient` and `ViewFor` give a consistent answer, never an exception, while `AddOwner`,
`RetireOwner`, `Reset` or `Restore` runs on another thread.

## Context

Found by reading the code while working BL-2122 on 2026-11-28; not reproduced.
In `Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.cs`, `LookUpRecipient` and `ViewFor` read `accounts` (and
`LookUpRecipient` reads it again through `MailDomainRouter`) without `storeLock`. `AddOwner`, `RetireOwner`, `Reset`
and `Restore` change it under the lock, and `LocalPartLookup.Remove` clears the dictionaries and adds the rest back.
A reader can so find no account for one that is configured, or hit a dictionary changed during the read. The class
summary says every operation is atomic with respect to every other.

Stand-in (root `CLAUDE.md`): a test adds or removes accounts while clients are sending mail.

## Acceptance criteria

- [ ] `LookUpRecipient` and `ViewFor` read `accounts`, `mailboxOwners` and the router's targets under `storeLock`.
- [ ] A test in `Surl.MailStore.UnitTests` runs both on several threads while another adds and retires an owner, and a configured account is found every time.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
