---
id: BL-868
title: Let a Surl.Api override decide a mail store write with async Deliver and Append
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-788, BL-811, BL-1035]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests, Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests, Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-868 — Let a Surl.Api override decide a mail store write with async Deliver and Append

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

- 2026-11-11 (lane 6): Code written but never built - `dotnet build` fails because global.json pins
  SDK 10.0.401 and only 10.0.100-rc.1 and 11.0.x are installed (BL-1035). The work is uncommitted and
  the shift stashes it. What it does:
  - `MailboxStore.DeliverAsync(recipients, message, cancellationToken)` and
    `AppendAsync(view, mailboxName, message, flags, internalDate, cancellationToken)` returning a new
    `MailAppendResult(Outcome, Stored)` record struct; both await the plan through `IsOutOfSpaceAsync`
    before `DeliverPending` / `AppendPending` take the lock. Sync methods kept.
  - Decision: an async plan answer that throws is served as no rule (as ADR-0124 decision 5 serves a
    throwing hook, and as the sync path already does); an `OperationCanceledException` propagates, so a
    closing session stores nothing. Record this as an amendment to ADR-0127 decision 5 (add
    `Documentation/Planning/Decisions/ADR-0127-...md` to touches; no Doing task names it).
  - `SmtpSession.DeliverAsync` and `ImapSession.Changes.AppendAsync` now call the async methods with the
    session's `CancellationToken`.
  - Thirteen new `MailboxStoreOutOfSpaceTests` (`DeliverAsync_*`, `AppendAsync_*`).
  - Still to do: build, fast tests, coverage, ADR amendment, update the library's CLAUDE.md line
    "(BL-868 makes the writes async)".

## Log

- 2026-11-05: Created by BL-788.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds; code is written and stashed by the shift
- 2026-11-12: Backlog -> Doing.
