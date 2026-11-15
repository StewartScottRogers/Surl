---
id: BL-1196
title: Read a mailbox's messages back through ComposedStores
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1194]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-15
completed:
---
# BL-1196 — Read a mailbox's messages back through ComposedStores

## Goal

`ComposedStores` lists an account's mailboxes and a mailbox's messages (UID, size, flags) and returns one message's bytes, as BL-1194's ADR names them, so every host can read back delivered mail.

## Context

- Decision: the ADR filed by BL-1194 (names, shapes, bounds).
- Where: `Application/Surl.Composition.UnitLibrary/ComposedStores.cs`, beside `SeedMailAsync`. The mail store is `MailboxStore` (`Protocols/Mail/Surl.MailStore.UnitLibrary`), which already has `ListMailboxes(MailView)`, `ReadMailbox(MailView, name, out MailboxSnapshot?)` and `FetchMessage(MailView, name, uid, out ReadOnlyMemory<byte>)`. Build the `MailView` for the account the ADR says, as the IMAP server builds one for a logged-in owner.
- A read publishes nothing, asks no fault rule, changes no `\Seen` flag, and works on a `--directory` mail store.
- Stand-in fit: the code under test sends mail over SMTP, and the test reads it back without an IMAP client. BL-1199, BL-1203 and BL-1206 build on this.

## Acceptance criteria

- [ ] `ComposedStores` has the mail read members the ADR names, documented with XML comments.
- [ ] A test in `Application/Surl.Composition.UnitTests` seeds two messages with `SeedMailAsync` and reads back their UIDs, sizes and exact bytes in delivery order.
- [ ] A test shows reading an unknown account or mailbox gives the ADR's not-found result, and a test shows a message larger than the ADR's bound is refused, not truncated.
- [ ] A test shows a read leaves the message's flags unchanged and publishes no `StoreChangedEvent`.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, the fast tests pass, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-15: Created.
