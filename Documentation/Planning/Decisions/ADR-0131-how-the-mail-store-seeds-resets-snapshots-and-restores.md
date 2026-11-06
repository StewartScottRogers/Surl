# ADR-0131 — How the mail store seeds, resets, snapshots and restores

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in
  BL-799, which built it.
- **Details:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 3 for `Surl.MailStore`'s
  `MailboxStore`.

## Context

ADR-0124 decision 3 gives every store seed, reset, snapshot and restore: seed through the protocol's own
write path without the fault plan; reset to the configured state with every `UIDVALIDITY` changed;
an opaque, bounded, in-memory snapshot that only its own instance can restore, with every `UIDVALIDITY`
changed again; stores on disk refused with `InvalidOperationException`; one `StoreChangedEvent` per reset
or restore. It left open the mail store's own shape: what seeding answers for a recipient SMTP would
refuse, what "configured" means for mailboxes, what the whole-store event names, what the snapshot type
is called and how it is bounded, and what a POP3 session holding its maildrop lock sees.

## Decision

1. **Seed** is `MailboxStore.Seed(recipientPath, bytes)`, returning `MailSeedResult(Recipient,
   Delivery)`. The recipient is resolved by `LookUpRecipient`, exactly as SMTP resolves `RCPT TO`, and
   `Recipient` is that answer. Only `Deliverable` stores anything: `NoSuchAccount` (which SMTP accepts
   and discards), `InvalidAddress` and `RelayingDenied` store nothing and leave `Delivery` null. A
   deliverable message goes through the delivery path SMTP's `DATA` takes - a pending message, the
   size, quota and store bounds, one copy per owner in each `INBOX` under its next UID, no flags, the
   clock's time as internal date, bytes kept exactly - but skips the fault plan, and publishes
   `Seeded` per copy where SMTP publishes `Delivered`. A store on disk is seeded like any write; the
   caller calls `SaveChangesAsync` as the servers do.
2. **Configured mailboxes** are the mailboxes each owner had when the constructor finished: its
   `INBOX`, its `folders=` and the shared folders. `Reset` empties every owner, then recreates exactly
   those, each empty with a new `UIDVALIDITY` and next UID 1; mailboxes created since are gone and
   configured ones deleted since are back. Aliases, catch-alls, quotas and shared-folder members are
   configuration, never changed.
3. **The snapshot type is `MailStoreSnapshot`**, named apart from `MailboxSnapshot` (one mailbox as
   IMAP's `SELECT` reports it). `TakeSnapshot` copies every owner's mailboxes (name, next UID) and
   messages (UID, flags, internal date) and shares each message's bytes, which never change once
   stored. It exposes `MessageCount`, `MailboxCount` and `ByteCount` (distinct message bytes, as the
   store counts them); the 256 MiB `MaxSnapshotBytes` bound across stores is the composed snapshot's
   to apply (BL-805), and a mail store's snapshot can never pass the store's own
   `MaxTotalMessageBytes`. Taking one changes and publishes nothing.
4. **Restore** refuses a snapshot another store took with `ArgumentException`, then replaces every
   owner's mailboxes with the snapshot's: the same messages, UIDs, flags, internal dates and next UIDs,
   each mailbox with a new `UIDVALIDITY`. Message, byte and mailbox counts are recounted, so the bounds
   hold after any number of restores.
5. **On disk** (a store made by `LoadAsync`), `Reset`, `TakeSnapshot` and `Restore` throw
   `InvalidOperationException` naming the index path, and change nothing. Both reset and restore also
   refuse, changing nothing, when the clock is so near 2106 that the mailboxes cannot all get a new
   `UIDVALIDITY`.
6. **Events.** `Reset` publishes one `StoreChangedEvent(Mail, Reset, "", 0, null)` and `Restore` one
   `StoreChangedEvent(Mail, Restored, "", snapshot.ByteCount, null)`: the empty name is the whole store,
   as `MailStoreChangeName` never gives it to a mailbox or message.
7. **Live sessions.** Reset and restore take the store's lock and swap the contents; nobody is waited
   for. An IMAP session sees the new contents and the changed `UIDVALIDITY` on its next command. Every
   POP3 maildrop lock is released, so a test's next POP3 login is never refused by a session left over
   from the previous test. A session that held a released lock still reads the messages it was given
   (their bytes stay pinned until it ends), and at `QUIT` removes only a message whose UID still holds
   the very bytes it was given - never a new message that reused the UID after a reset. When it ends it
   releases the lock only if it still holds it, never one a later session took.

## Alternatives considered

- **Seed as `Append` into any mailbox with flags.** More reach, but ADR-0124 says seeding uses the path
  a protocol uses, and for mail that is delivery: routing, aliases and quotas apply as they would to
  SMTP. A test wanting a flagged message in a folder can still log in over IMAP.
- **Keep POP3 locks across a reset.** The lock's fixed view would point at messages that no longer
  exist, and the next test's POP3 login would fail with `[IN-USE]` until the old session timed out.
- **Copy message bytes into the snapshot.** Doubles memory for nothing: stored bytes are immutable.

## Consequences

- `MaildropLock` removes by UID and bytes together, and `MailboxStore` keeps which lock holds each
  maildrop.
- BL-805 composes this store's operations into `ComposedStores` and applies `MaxSnapshotBytes`.
