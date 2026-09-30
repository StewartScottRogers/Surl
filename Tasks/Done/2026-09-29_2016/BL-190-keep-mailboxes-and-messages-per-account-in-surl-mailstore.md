---
id: BL-190
title: Keep mailboxes and messages per account in Surl.MailStore
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-189]
touches: [Surl.MailStore.UnitLibrary, Surl.MailStore.UnitTests]
requirement: FR-047
created: 2026-09-29
completed: 2026-09-29
---
# BL-190 — Keep mailboxes and messages per account in Surl.MailStore

## Goal

`Surl.MailStore` keeps, in memory, the mailboxes and messages BL-184's ADR models - delivery,
append, fetch, flags, expunge, mailbox create/delete/rename, UIDs and `UIDVALIDITY` - bounded and
safe for concurrent SMTP, IMAP and POP3 sessions, so the three servers share one store.

## Context

- Decision: BL-184's ADR (the model, recipient mapping, the POP3 maildrop lock, the bounds and
  their constants, what a delivery past a bound reports).
- Shape: a public store type the servers take in their constructors (as
  `Surl.Protocol.Mqtt.UnitLibrary/MqttRetainedMessages.cs` is shared across MQTT connections),
  with typed outcomes rather than exceptions for refusals a peer causes (mailbox missing, bound
  reached, maildrop locked). Internal dates from the injected `TimeProvider`.
- Concurrency: operations are atomic with respect to each other; a POP3 session's view is fixed
  at login as the ADR says; UIDs are strictly ascending per mailbox (RFC 3501 section 2.3.1.1).
- Persistence is BL-191's; this task leaves a seam for it (e.g. a change notification or an
  injected writer), as the ADR says.

## Acceptance criteria

- [x] Fast tests cover: delivery to an account's `INBOX`; recipient mapping and an unknown
      recipient as the ADR says; append to a named mailbox and to a missing one; UIDs ascending
      and never reused after expunge; `UIDVALIDITY` stable; flags set and cleared; expunge;
      create, delete and rename of mailboxes (and `INBOX`'s special cases); each bound refusing
      the delivery that would pass it; the maildrop lock held and released; concurrent deliveries
      from several tasks losing nothing.
- [x] `Surl.MailStore.UnitLibrary.csproj` references only what BL-184's ADR allows;
      `ProtocolIsolationTests` pass.
- [x] `dotnet build Surl.MailStore.UnitLibrary -warnaserror` is clean; the fast tests pass;
      `Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary` reports 100% line and branch
      coverage and no failing member.

## Notes

Plan: ADR-0050 decisions 2 to 6 are the design; persistence (decision 7) is BL-191's. Built
`MailboxStore` with typed `MailStoreOutcome`s, `MailRecipient` (from `LookUpRecipient`),
`MailView` (from `ViewFor`), `MaildropLock`, and the internal `OwnerMailboxes`,
`StoredMailbox`, `StoredMessage`, `MessageBody` (shared, reference-counted bytes),
`MailboxName` and `MailRecipientPath`. One `Lock` guards every operation. 141 tests;
`Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary`: 100% line, 100% branch,
86 members, 0 failing, worst CRAP 10.

Choices, each within ADR-0050 and decided by Claude under Stewart's delegation:
- **Type name `MailboxStore`, not `MailStore`:** inside namespace `Surl.MailStore`, a type
  `MailStore` is shadowed by the namespace for every caller in another `Surl.*` namespace.
  The ADR's constants exist with its values on `MailboxStore`. ADR-0050 could not be edited
  here (BL-154 holds `Documentation/Planning/Decisions`), so BL-225 renames them in the ADR.
- **The store's own message bound** (`maxMessageBytes`, 0 = no limit, as
  `ExchangeLimits.MaxUploadBytes`) refuses with a new `MessageTooLarge` outcome rather than
  `StoreFull`, so a server can answer it as ADR-0006's too-large reply.
- **A renamed non-INBOX mailbox gets a new `UIDVALIDITY`** (keeping its UIDs and next UID), so
  a client that cached a mailbox under the new name never sees UIDs reused against an old
  `UIDVALIDITY`. Renaming `INBOX` follows RFC 3501 section 6.3.5 as the ADR says.
- **UIDs:** a mailbox gives UIDs up to `uint.MaxValue - 1`, so its next UID (`UIDNEXT`) always
  fits a `uint`; past that is `StoreFull`. `UIDVALIDITY` past 2106 (`uint` seconds) is
  `StoreFull` for a new mailbox and an `InvalidOperationException` at construction.
- **RCPT paths:** angle brackets optional, source route ignored, quoted local part unquoted,
  domain required (non-empty, no space, control, `<`, `>` or `@`) and ignored, `<Postmaster>`
  without a domain valid, characters above U+007F allowed (SMTPUTF8, RFC 6531).
- **Empty view** (a name that is no account): no mailboxes; every write is `MailboxMissing`;
  `LockMaildrop` gives an empty maildrop that locks nothing.
- **Persistence seam:** `ChangeCount` rises once per change that alters the store and not for
  a refused or no-op one; BL-191 writes the index there.

Review (code-reviewer) found no blocking defect; its `UIDVALIDITY` wrap past 2106 is fixed
above. Accepted as the ADR has it: a POP3 view keeps expunged messages' bytes alive, so
memory can exceed `MaxTotalMessageBytes` while a maildrop lock is held (decision 4 makes an
expunged message readable from the view). `LookUpRecipient` and `ViewFor` read the account
maps without the lock because nothing changes them after construction; BL-191 must keep that
true or take the lock.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. MailboxStore keeps mailboxes and messages per owner in memory: delivery, append, fetch, flags, expunge, copy, create/delete/rename, UIDs, UIDVALIDITY, bounds and the POP3 maildrop lock, 100% covered
