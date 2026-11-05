---
id: BL-788
title: Publish mail store changes and answer the out-of-space fault in Surl.MailStore
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-05
completed:
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

- [ ] Named `MailboxStoreTests` show one event per change kind above with its owner, mailbox and UID, in the order
      the changes happened, from a `RecordingSurlEventPublisher` (or the ADR's name for BL-823's double).
- [ ] Named tests show an out-of-space rule for one mailbox makes delivery to it answer the ADR's outcome and
      publish the fault event, while delivery to another mailbox succeeds; a rule with `times=1` fires once.
- [ ] With no publisher or plan given, every existing `Surl.MailStore.UnitTests` test passes unchanged.
- [ ] `dotnet build Protocols/Mail/Surl.MailStore.UnitLibrary -warnaserror` is clean, the fast tests are green,
      and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
