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
completed: 2026-11-28
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

- [x] `LookUpRecipient` and `ViewFor` read `accounts`, `mailboxOwners` and the router's targets under `storeLock`.
- [x] A test in `Surl.MailStore.UnitTests` runs both on several threads while another adds and retires an owner, and a configured account is found every time.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `ViewFor` already read `accounts` and `mailboxOwners` under `storeLock` (BL-2129 added that lock), so only
  `LookUpRecipient` changed: it now takes `storeLock` around both ways of resolving, the router's included.
- `anonymousOwner` and `router` are still read outside the lock: both are set only while the store is made.
- The test is `MailboxStoreRunTimeOwnerTests.LookUpRecipientAndViewFor_WhileAnOwnerIsAddedAndRetiredOnAnotherThread_FindAConfiguredAccountEveryTime`,
  with and without a mail domain. Before the fix it failed in both (26 and 167 misses in the first reader).
- No ADR: this makes the class summary's "every operation is atomic" true; no behaviour was decided.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. LookUpRecipient resolves recipients under the store's lock, so adding, retiring, resetting or restoring accounts on another thread no longer hides a configured account.
