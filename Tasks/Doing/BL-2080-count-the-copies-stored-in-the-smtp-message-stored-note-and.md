---
id: BL-2080
title: Count the copies stored in the SMTP Message stored note and write no note when none is
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-2066]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2080 — Count the copies stored in the SMTP Message stored note and write no note when none is

## Goal

The SMTP server's `Message stored: <n> bytes for <k> recipients` note counts the copies stored, and is not written
when none is.

## Context

ADR-0053 Amendment 3 (BL-2066, `Documentation/Planning/Decisions/`) decides it. The note goes to the verbose log
only and no wire byte depends on it, so no upstream curl measurement is needed.

In `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.cs`, `DeliverAsync` writes
`accepted.Deliverable.Count` for `<k>` after every successful delivery (read on 2026-11-28). Its `owners`
parameter, `SmtpMailTransaction.DeliverablePerOwner()`'s list, already holds one element per copy stored, with the
option `--mail-delivered-to` or without, and is empty when every accepted `RCPT` was discarded.

Unchanged: the text of the note, `1 recipients` included; `<n>`, the longest pending message's length; the discard
notes; every reply, stored byte and `StoreChangedEvent`; and the call that saves the mail store.

Tests that pin the note, all in `Protocols/Mail/Surl.Protocol.Smtp.UnitTests/` (every match of `Message stored` in
the repository's test code on 2026-11-28):

| File and test method | Expected note today | After |
| --- | --- | --- |
| `SmtpDeliveryTests.cs`, `ServeAsync_NoRecipientNamesAnAccount_StoresNothingAndAnswers250` | `... for 0 recipients`, the last note | no `Message stored` note; the only note is `Mail for nobody@x discarded: no such account` |
| `SmtpMailDomainTests.cs`, `ServeAsync_OwnerNamedByAnAddressAndAnAlias_GetsOneCopyWhicheverComesFirst` | `... for 1 recipients` | `... for 2 recipients`, in each of its three recipient lists |
| `SmtpDeliveryTests.cs`, `ServeAsync_Accounts_DeliverOneCopyPerAccountAndDiscardTheUnknown` | `... for 1 recipients` | unchanged (one account named twice, two unknown) |

Stand-in: no new event, option, seed, reset or fault; the note then agrees with the delivery events a test already
sees.

## Acceptance criteria

- [ ] `ServeAsync_NoRecipientNamesAnAccount_StoresNothingAndAnswers250` asserts that the log's notes are exactly `Mail for nobody@x discarded: no such account`, and was seen to fail before the fix (said in the Log).
- [ ] `ServeAsync_OwnerNamedByAnAddressAndAnAlias_GetsOneCopyWhicheverComesFirst` expects `for 2 recipients`, and was seen to fail before the fix (said in the Log).
- [ ] New tests in `SmtpMailDomainTests`, `ServeAsync_AliasToTwoOwners_NotesTwoRecipients` (`RCPT team@`) and `ServeAsync_PartlyOverlappingAliases_NoteCountsThreeRecipients` (`team@`, then `pair@`), assert the note says `for 2 recipients` and `for 3 recipients`.
- [ ] A new `ServeAsync_AnonymousOwnerTwoRecipients_NotesTwoRecipients` in `SmtpDeliveryTests` (two accepted `RCPT`s under `--allow-anonymous`) asserts the note says `for 2 recipients` and the anonymous `INBOX` holds two messages.
- [ ] `ServeAsync_Accounts_DeliverOneCopyPerAccountAndDiscardTheUnknown` and every other existing test not named in the first two criteria pass with their expected values unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green without `TestCategory=Integration`, and the changed code has 100% line and branch coverage.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
