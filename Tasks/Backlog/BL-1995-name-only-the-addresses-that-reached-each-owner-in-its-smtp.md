---
id: BL-1995
title: Name only the addresses that reached each owner in its SMTP Delivered-To copy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1995 — Name only the addresses that reached each owner in its SMTP Delivered-To copy

## Goal

With `--mail-delivered-to`, each owner's stored SMTP copy, and the `DeliveredFor` of its delivery
`StoreChangedEvent`, names only the envelope addresses that reached that owner.

## Context

Found while working BL-1978 and confirmed by reading the code on 2026-11-27. One recipient, including a `Bcc`-like
one, can be revealed to another. This contradicts ADR-0053 Amendment 1 decisions 3 and 4
(`Documentation/Planning/Decisions/`): the SMTP server "builds one pending message per owner, each with only that
owner's addresses" and "an owner reads only the addresses that reached it, never another owner's". The decision
stands, so no new ADR is needed. `Delivered-To` is Surl's own field (RFC 9228), so no upstream curl run backs it.

Cause, in `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/`: `SmtpSession.ReceiveMessageAsync` builds one pending
message per element of `SmtpMailTransaction.Deliverable`. An alias element names several owners who share that copy,
and `SmtpMailTransaction.PathsReaching` returns every path that reached any of them.

Examples with the aliases of `SmtpDeliveredToTests` (`team@` is ann and bob, `pair@` is bob and cat):

| `RCPT`s, in order | ann's copy names today | Each copy must name |
| --- | --- | --- |
| `team@`, `pair@` | `team@`, `pair@` | ann `team@`; bob `team@`, `pair@`; cat `pair@` |
| `bob@`, `team@` | `bob@`, `team@` | ann `team@`; bob `bob@`, `team@` |

The passing test `ServeAsync_PartlyOverlappingAliases_EachCopyNamesTheAddressesThatReachedItsOwners` pins the first
wrong answer.

- `Surl.MailStore.UnitLibrary` needs no change: the public `MailRecipient.WithoutOwners`, given an element's other
  owner names, yields a recipient of one owner, and `MailOwnerCopy` takes that with its own `PendingMessage` and
  `deliveredFor` list.
- Unchanged: one copy per owner per message (ADR-0102 decision 1.3); one copy and own path per recipient for the
  anonymous owner; one all-or-none `DeliverAsync` batch; the longest copy deciding `--max-filesize` (decision 5);
  the replies and the `Message stored: ... for N recipients` note, N still counting `Deliverable` elements; and, with
  the option off, the stored bytes, the shared message file and the events.
- With the option on, an alias's owners no longer share a message file, so each copy counts against the store's total
  message bytes, as decision 4 ("Each owner's copy is therefore its own message") says.
- Stand-in: the shared `MailboxStore` and its existing events; no new option, seed, reset or fault. A test observes
  the fix through the stored bytes (IMAP or POP3 read the same store) and `StoreChangedEvent.DeliveredFor`.
- The library's `CLAUDE.md` ("Mail" bullet) and XML comments describe today's per-element behaviour. BL-1977 and
  BL-1978, in `Doing` when this was filed, edit those sentences, so start from what they wrote.

## Acceptance criteria

- [ ] In `SmtpDeliveredToTests`, the partly overlapping aliases test, renamed `ServeAsync_PartlyOverlappingAliases_EachCopyNamesOnlyTheAddressesThatReachedItsOwner`, and a new `ServeAsync_RecipientThenAliasHoldingIt_OtherOwnersCopyOmitsTheRecipient` assert the table's two rows for every owner; both were seen to fail before the fix (said in the Log) and pass after it.
- [ ] A new `ServeAsync_PartlyOverlappingAliases_EachDeliveryEventNamesOnlyItsOwnersAddresses` asserts that each owner's delivery `StoreChangedEvent.DeliveredFor` lists exactly the addresses in the first row, in `RCPT` order.
- [ ] A new `ServeAsync_OneAliasOwnerOverQuota_StoresNothingForAnyOwner` (`RCPT team@`, ann over quota) is answered `452 4.2.2 Mailbox full` and stores nothing for ann or bob.
- [ ] The other existing tests in `Surl.Protocol.Smtp.UnitTests` pass with their expected values unchanged, including `ServeAsync_OptionOff_StoresTheBytesItAlwaysDid`.
- [ ] The library's `CLAUDE.md` and the XML and code comments in the library that describe `Delivered-To` delivery say each owner has its own pending message naming only the paths that reached that owner; none says an alias's owners share a copy with the option on.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green without `TestCategory=Integration`, and the changed code has 100% line and branch coverage.

## Notes

- Today `SmtpSession.DeliverAsync` sets no `DeliveredFor` at all, with the option on or off: it builds each
  `MailOwnerCopy` without a `deliveredFor` list and its recipients record no addresses (checked 2026-11-27). So with
  the option on this task must pass each copy its list. The option-off events are BL-1996, which depends on this
  task.

## Log

- 2026-11-27: Created.
