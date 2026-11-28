---
id: BL-2000
title: Give each owner of an SMTP message only the envelope addresses that reached that owner
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1977, BL-1978]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2000 — Give each owner of an SMTP message only the envelope addresses that reached that owner

## Goal

An owner's `Delivered-To` lines and its delivery event's `DeliveredFor`, for mail delivered over SMTP, hold exactly
the `RCPT` paths that resolved to that owner, so no owner reads another owner's address.

## Context

Privacy defect found while working BL-1977 (2026-11-27), checked by reading the code that day, not by running it.
Files are in `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/` unless another path is given.

ADR-0053 Amendment 1 decision 4
(`Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md`): with
`--mail-delivered-to` the SMTP server builds one pending message per owner, each with only that owner's addresses,
and delivers them as one batch; an owner never reads another owner's. ADR-0102 decision 1.3: one copy per owner per
message. The code builds one pending message per element of `SmtpMailTransaction.Deliverable`, and an alias element
can name several owners.

1. `RCPT` `a@dom`, then `RCPT` `team@dom` (an alias for the owners `a` and `b`).
   `SmtpMailTransaction.KeepNamedRecipient` replaces `a` with the alias, so `Deliverable` is `[team]`;
   `PathsReaching(team)` returns both paths; `SmtpSession.ReceiveMessageAsync` delivers one pending message to both
   owners, and `b`'s copy carries `Delivered-To: a@dom`.
2. The test `ServeAsync_PartlyOverlappingAliases_EachCopyNamesTheAddressesThatReachedItsOwners` in
   `Protocols/Mail/Surl.Protocol.Smtp.UnitTests/SmtpDeliveredToTests.cs` pins the same leak: `RCPT` `team@` (Ann,
   Bob), then `pair@` (Bob, Cat), and it expects Ann's copy to name `pair@`, which does not reach Ann. Change its
   expected values; Ann's copy names `team@`, Bob's `team@` then `pair@`, Cat's `pair@`.
3. Decision 7's `StoreChangedEvent.DeliveredFor` does not leak, because the SMTP server never sets it, with the
   option or without: `MailboxStore.LookUpRecipient` records no addresses, `SmtpSession.DeliverAsync` calls
   `new MailOwnerCopy(recipient, pendings[index])` with no `deliveredFor`, and nothing calls
   `MailRecipient.ForAddresses`. Decision 7 says the event names each owner's addresses whatever the option, so set
   it in both paths.

The `Surl.MailStore` public API already allows the fix with no change there: `MailRecipient.WithoutOwners` given
every owner name but one yields a single-owner recipient; `MailRecipient.ForAddresses` records its addresses for
the single-message `DeliverAsync`; `MailOwnerCopy` takes a `deliveredFor` list. The anonymous owner still gets one
copy per recipient, naming that recipient's own path.

Documents in the same diff: the "**Delivery.**" paragraph of the `SmtpProtocolServer` XML remarks (BL-1977 wrote it
to describe the per-recipient behaviour), the doc comment of `SmtpMailTransaction.PathsReaching` (or of what
replaces it), the `//` comment above `SmtpSession.ReceiveMessageAsync`, and the "Mail" bullet of the library's
`CLAUDE.md` (BL-1978 writes there that each deliverable recipient has its own pending message). Checked on
2026-11-27 and needing no change: the `--mail-delivered-to` paragraph in
`Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` and the doc comment of `MailboxStore.DeliveredToEnabled` already
describe one copy per owner. BL-2001 corrects other comments in these files afterwards.

Stand-in: the shared mail store; the event is the store's delivery `StoreChangedEvent`; seeding, reset and the
out-of-space and command-stage faults are the existing ones. The field is Surl's own (RFC 9228), so no upstream curl
run backs it.

## Acceptance criteria

- [x] A test in `SmtpDeliveredToTests`, seen to fail before the fix, sends `RCPT` for Ann then for `team@` with the option on: Ann's inbox holds one copy with `Delivered-To` lines for Ann's address then `team@`, and Bob's one copy with the `team@` line only.
- [x] The partly overlapping aliases test expects the three copies in Context item 2, and failed on Ann's copy before the fix.
- [x] Tests show that each `Delivered` event's `DeliveredFor` holds exactly the paths, in `RCPT` order, that resolved to that event's owner, for the Ann then `team@` case, with the option on and with it off.
- [x] With the option off the stored bytes are unchanged (`ServeAsync_OptionOff_StoresTheBytesItAlwaysDid` passes), and with it on an owner over quota still stores nothing for any owner.
- [x] The `SmtpProtocolServer` delivery remarks, the paths method's doc comment and the library `CLAUDE.md` say that each owner has its own pending message naming only the paths that resolved to that owner.
- [x] `dotnet build Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary -warnaserror` is clean; `Surl.Protocol.Smtp.UnitTests` fast tests pass with 100% line and branch coverage of the change, none with `TestCategory=Integration`.

## Notes

- Plan: the task's Context was the plan. `SmtpMailTransaction.DeliverablePerOwner` replaces `PathsReaching`: it splits
  `Deliverable` into one `MailRecipient` per owner (`WithoutOwners`), each recording its own paths (`ForAddresses`).
  `SmtpSession` delivers those recipients on both paths, so `DeliveredFor` is set with the option off (one shared
  pending message, bytes unchanged) and on (one `MailOwnerCopy` and `Delivered-To` set per owner). No change to
  `Surl.MailStore`, no new ADR: ADR-0053 Amendment 1 decisions 3, 4 and 7 already say this.
- Default taken: the `Message stored: ... for N recipients` note still counts `Deliverable` elements.
- With the option on, an alias's owners no longer share a message file, so each copy counts against the store's total
  message bytes (ADR-0053 Amendment 1 decision 4).
- Six new or changed test runs in `SmtpDeliveredToTests` failed before the fix and pass after it. The partly
  overlapping aliases test is now `..._EachCopyNamesOnlyTheAddressesThatReachedItsOwner`.
- Gates (2026-11-27): solution build clean, 30385 fast tests passed, SMTP fast tests 303 passed,
  `Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary` 0 failing members, `dotnet format` clean on both
  SMTP projects, `UpstreamCurlSendsMailToSurlOverSmtpTests` (pinned curl against a live surl) 39 passed.
  No separate reviewer or conformance agent: no wire byte changes, and the diff was reviewed in the session.
- BL-1995 and BL-1996, filed from BL-1978, ask for this same behaviour and are moved to Deferred as duplicates. They
  name a few further tests (`RCPT` `bob@` then `team@`; option-off events for partly overlapping aliases; one alias
  owner over quota) that were not written here.
- The stale glossary row is BL-1999, already filed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Each SMTP owner's Delivered-To lines and delivery event DeliveredFor name only the RCPT paths that resolved to that owner.
