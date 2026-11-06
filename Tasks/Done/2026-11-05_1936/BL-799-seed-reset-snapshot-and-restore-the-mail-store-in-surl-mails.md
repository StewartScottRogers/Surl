---
id: BL-799
title: Seed, reset, snapshot and restore the mail store in Surl.MailStore
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-788]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-799 — Seed, reset, snapshot and restore the mail store in Surl.MailStore

## Goal

A test can put a message into a mailbox before its client connects (seed), return the mail store to its
configured state between tests (reset), and take and restore an in-memory snapshot, each through
`MailboxStore` itself, each published on the event stream.

## Context

- Decision: BL-822's ADR decision 3 (what reset returns to - planning default: the configured owners, mailboxes,
  aliases, quotas and shared folders exist, holding no messages; what a snapshot holds and its bound; what each
  does when the store is on disk under `<path>/.surl/mail`).
- Where: `Protocols/Mail/Surl.MailStore.UnitLibrary` (`MailboxStore.cs`, `MailboxSnapshot.cs` already exists for
  IMAP's view - do not confuse the two; name the new type as the ADR does). Seeding goes through the same delivery
  path SMTP uses, so routing, quotas, UIDs and flags follow ADR-0050 and ADR-0102 exactly; a seeded message is
  stored byte for byte (ADR-0050 decision 3).
- Each operation publishes the `StoreChangedEvent`s BL-788 defined (or one summary event per reset or restore, as
  the ADR says). Operations are safe while IMAP and POP3 sessions are open: a session holding a maildrop lock
  (POP3) or a selected mailbox (IMAP) sees what the ADR says (planning default: reset and restore wait for no one
  and the next command sees the new state; POP3's lock is released).
- Stand-in: store = the mail store; the controllable principle. Composition exposes it in BL-805; `Surl.Api` in
  BL-810.

## Acceptance criteria

- [x] Named `MailboxStoreTests`: a seeded message is found by IMAP's and POP3's read paths with the next UID and
      its exact bytes; seeding to an unknown address answers the same outcome SMTP would; reset leaves configured
      mailboxes empty and UID validity as the ADR says; snapshot, change, restore gives back exactly the
      snapshot's messages, flags and UIDs; each publishes its events.
- [x] A named test shows the disk-backed behaviour the ADR decides (done, refused with a named outcome, or
      in-memory only).
- [x] `dotnet build Protocols/Mail/Surl.MailStore.UnitLibrary -warnaserror` is clean, the fast tests are green,
      and the library keeps 100% line and branch coverage.

## Notes

- Built in `MailboxStore.Control.cs`: `Seed` (returns `MailSeedResult`), `Reset`, `TakeSnapshot`
  (returns `MailStoreSnapshot`, named apart from IMAP's `MailboxSnapshot`) and `Restore`. Decisions
  ADR-0124 left open are recorded in ADR-0131 (decided by Claude under Stewart's delegation).
- `touches` gained `Documentation/Planning/Decisions` for ADR-0131 and its README row; no task in
  Doing names it (BL-803 touches only `Surl.Composition`).
- Seed resolves the recipient exactly as SMTP's `RCPT TO` (`LookUpRecipient`) and stores only for
  `Deliverable`; `NoSuchAccount`, `InvalidAddress` and `RelayingDenied` store nothing and leave
  `Delivery` null.
- Reset and restore release every POP3 maildrop lock. Found while building: after a reset, UIDs restart
  at 1, so a left-over POP3 session's `QUIT` could have deleted a new message that reused a UID, and its
  dispose could have released a later session's lock. `MaildropLock` now removes by UID and bytes
  together, and the store releases a lock only for the session that holds it.
- Disk-backed: `Reset`, `TakeSnapshot` and `Restore` throw `InvalidOperationException` naming the index
  path (`OnDisk_SeedIsWrittenButResetSnapshotAndRestoreAreRefused`); seeding writes as SMTP does.
- `MaxSnapshotBytes` across stores is BL-805's; the snapshot exposes `ByteCount` for it.
- Coverage measured with the MSTest coverage collector: `Surl.MailStore.UnitLibrary` line 100%,
  branch 100%. 14 new tests in `MailboxStoreTests` (18 with data rows).

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. MailboxStore seeds through the delivery path, resets to its configured mailboxes, and takes and restores in-memory snapshots, each published on the event stream; disk stores refuse reset, snapshot and restore (ADR-0131)
