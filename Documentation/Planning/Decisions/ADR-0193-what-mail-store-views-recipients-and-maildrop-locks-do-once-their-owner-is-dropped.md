# ADR-0193 — What mail store views, recipients and maildrop locks do once their owner is dropped

- **Status:** Accepted
- **Date:** 2026-11-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-28, in BL-2122.
- **Builds on:** [ADR-0131](ADR-0131-how-the-mail-store-seeds-resets-snapshots-and-restores.md) decision 7,
  [ADR-0185](ADR-0185-the-mail-store-reaches-an-account-added-while-the-server-runs-and-retires-one-removed.md)
  decisions 3 and 4.

## Context

ADR-0185 lets `Reset` drop an owner added while the server ran, and `Restore` drop one the snapshot does not
hold. A session open across that call still holds the owner's `MailView`, `MailRecipient` or `MaildropLock`. No
ADR said what those do afterwards. `MaildropLock.RemoveMessages`, `LockMaildrop` and `Deliver` threw
`KeyNotFoundException`, and `CreateMailbox` answered `Succeeded` while writing into an owner the store no longer
held. A test that resets the store between scenarios while client sessions are open (ADR-0124) hits all four.

## Decision

A handle to a dropped owner stays usable, never throws for that reason, and never writes to the owner.

1. **A view** has no mailboxes of its own. Reading or writing one is `MailboxMissing`, `CreateMailbox` included,
   and `LockMaildrop` succeeds with an empty maildrop that locks nothing, as for a view with no owner. Shared
   folders it is a member of are not the dropped owner's and stay reached.
2. **A recipient** gives the dropped owner no copy. The other owners of the same delivery get theirs, and the
   dropped one is left out of the quota, bound and out-of-space checks. A delivery with no owner left answers
   `Succeeded`, stores and publishes nothing and does not raise `ChangeCount`: SMTP accepts and discards, as it
   does for a recipient that names no account (ADR-0102 decision 1.3).
3. **A maildrop lock** removes nothing and returns 0. It still reads the messages it was given and is disposed as
   usual (ADR-0131 decision 7).
4. **The handle means that owner, not that name.** An account added again under the name after a reset is a new
   owner, which old handles do not reach, so a session from one scenario cannot write into the next one's mail. A
   `Restore` that brings the dropped owner itself back makes its handles reach it again.

## Alternatives considered

- **A new `MailStoreOutcome` for a delivery to a dropped owner.** SMTP, which has already accepted the
  recipient, would need a reply for a case that exists only while a test rewinds the store, and it would refuse
  the message for the other recipients too.
- **Look the owner up again by name.** A message from before a reset would appear in the store after it, which
  breaks the clean state a test resets to.

## Consequences

- `Reset` and `Restore` are safe with sessions open: every mail store operation answers with a typed outcome.
