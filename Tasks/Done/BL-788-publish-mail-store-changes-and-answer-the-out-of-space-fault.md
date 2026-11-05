---
id: BL-788
title: Publish mail store changes and answer the out-of-space fault in Surl.MailStore
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-788 — Publish mail store changes and answer the out-of-space fault in Surl.MailStore

## Goal

`MailboxStore` publishes every change it makes - a message delivered, appended, copied, flagged, expunged or
deleted, a mailbox created, renamed or deleted - on the event stream, and answers a delivery or append with its
existing full outcome when an out-of-space fault rule matches the mailbox, so a test can watch mail arrive and
make delivery fail on purpose.

## Context

- Decision: BL-822's ADR decisions 1 (`StoreChangedEvent`'s mail fields) and 4 (out of space: scope by account,
  address or mailbox; which outcome it answers).
- Contracts: BL-823 (`ISurlEventPublisher`, `IFaultPlan`, `FaultStage` for a store write).
- Where: `Protocols/Mail/Surl.MailStore.UnitLibrary` (`MailboxStore.cs` and its write paths). It already
  references `Surl.Protocol.Abstractions`. Take the publisher and the fault plan as optional constructor
  parameters defaulting to the null objects, so `Surl.Composition` and every existing test compile unchanged;
  BL-803 passes the real ones.
- Out of space answers `MailStoreOutcome.StoreFull` (or `QuotaExceeded`, as the ADR says), which SMTP, IMAP and
  POP3 already word in their own protocols, so no server changes here. Publish a `FaultAppliedEvent` when it fires.
- An event carries the owner, mailbox and UID, and never the message body unless the ADR says otherwise; the time
  comes from the store's `TimeProvider` if it has one, else the publisher's.
- Stand-in: store = the mail store (ADR-0100 decision 1); publishes store changes; seeding and reset are BL-799.

## Acceptance criteria

- [x] Named `MailboxStoreTests` show one event per change kind above with its owner, mailbox and UID, in the order
      the changes happened, from a `RecordingSurlEventPublisher` (or the ADR's name for BL-823's double).
- [x] Named tests show an out-of-space rule for one mailbox makes delivery to it answer the ADR's outcome and
      publish the fault event, while delivery to another mailbox succeeds; a rule with `times=1` fires once.
- [x] With no publisher or plan given, every existing `Surl.MailStore.UnitTests` test passes unchanged.
- [x] `dotnet build Protocols/Mail/Surl.MailStore.UnitLibrary -warnaserror` is clean, the fast tests are green,
      and the library keeps 100% line and branch coverage.

## Notes

- Plan: ADR-0124 decisions 1 and 4 with BL-823's contracts as built; the choices they left open are
  ADR-0127, decided by Claude under Stewart's delegation:
  - BL-823's `StoreChangedEvent` has no owner or UID field, so `Name` carries them in a documented shape,
    `MailStoreChangeName`: `<owner>/<mailbox>` and `<owner>/<mailbox>/;UID=<uid>` (RFC 5092's path).
  - Out of space answers `QuotaExceeded` (ADR-0124: "the outcome it already has for a full quota"), asked
    per distinct owner and mailbox at `StoreWrite`, before the store's lock; the store publishes the
    `FaultAppliedEvent` because it is what applies the fault.
  - `At` is left to the stream to stamp (ADR-0124 decision 1, as amended after this task was written),
    not the store's `TimeProvider`; `ExchangeId` is null because the store is not told the exchange.
  - The writes are synchronous, so a plan answer still pending (a `Surl.Api` override) is served as no
    rule, as ADR-0124 decision 5 serves a timed-out hook. Filed BL-860 for async `Deliver`/`Append`.
- Tests are `MailboxStoreEventTests` (11) and `MailboxStoreOutOfSpaceTests` (11), split by concern
  rather than one `MailboxStoreTests`, as the project already splits its store tests. `times=1` is shown
  with `SpendingFaultPlan`, a test double that spends `Times` as BL-792's matcher will, since BL-823's
  `ScriptedFaultPlan` never does.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0127; no task in `Doing` names it.
- `Deliver` and `Append` were split (`DeliverUnlessOutOfSpace`, `AppendPending`) to stay at measured
  complexity 10 or less. `Measure-CodeQuality.ps1` still flags `ReachOwners` (16) and the
  `MailDomainConfiguration` constructor (12), both untouched here and below the build's `CA1502` gate.
- Measured: `Surl.MailStore.UnitTests` 345 passed; library 100% line and branch.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. MailboxStore publishes every mail change on the event stream and answers an out-of-space fault rule on delivery or append with QuotaExceeded
