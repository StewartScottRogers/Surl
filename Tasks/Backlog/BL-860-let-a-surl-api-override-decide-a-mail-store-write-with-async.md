---
id: BL-860
title: Let a Surl.Api override decide a mail store write with async Deliver and Append
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-788, BL-811]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests, Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests, Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-860 — Let a Surl.Api override decide a mail store write with async Deliver and Append

## Goal

A `Surl.Api` response override (BL-811) that answers asynchronously can make an SMTP delivery or an IMAP
`APPEND` fail with the out-of-space fault, because the mail store consults the fault plan by awaiting it.

## Context

- ADR-0127 decision 5: `MailboxStore.Deliver` and `Append` are synchronous, so they take only an answer
  `IFaultPlan.FindAsync` gives at once and serve a pending one as if no rule matched. ADR-0124 decision 5
  lets an override decide any fault kind, `out-of-space` at `StoreWrite` included.
- Where: add `DeliverAsync(recipients, message, cancellationToken)` and an `AppendAsync` overload (it cannot
  have an `out` parameter, so it returns the outcome and the `MailStoredUid` together) to
  `Protocols/Mail/Surl.MailStore.UnitLibrary`, awaiting the plan before the store's lock; switch
  `SmtpSession` (`mailStore.Deliver`) and `ImapSession.Changes` (`mailStore.Append`) to them, passing the
  session's cancellation token. Keep the synchronous methods for existing callers.

## Acceptance criteria

- [ ] Named `MailboxStoreOutOfSpaceTests` show `DeliverAsync` and `AppendAsync` answer `QuotaExceeded` and
      publish `FaultAppliedEvent` when the plan's answer completes later with an `OutOfSpace` rule.
- [ ] `SmtpSession` and `ImapSession` call the async methods; their existing tests pass unchanged.
- [ ] The fast tests are green, and `Surl.MailStore`, `Surl.Protocol.Smtp` and `Surl.Protocol.Imap` keep
      100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created by BL-788.
