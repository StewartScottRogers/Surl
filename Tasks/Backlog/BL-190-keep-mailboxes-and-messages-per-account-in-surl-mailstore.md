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
completed:
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

- [ ] Fast tests cover: delivery to an account's `INBOX`; recipient mapping and an unknown
      recipient as the ADR says; append to a named mailbox and to a missing one; UIDs ascending
      and never reused after expunge; `UIDVALIDITY` stable; flags set and cleared; expunge;
      create, delete and rename of mailboxes (and `INBOX`'s special cases); each bound refusing
      the delivery that would pass it; the maildrop lock held and released; concurrent deliveries
      from several tasks losing nothing.
- [ ] `Surl.MailStore.UnitLibrary.csproj` references only what BL-184's ADR allows;
      `ProtocolIsolationTests` pass.
- [ ] `dotnet build Surl.MailStore.UnitLibrary -warnaserror` is clean; the fast tests pass;
      `Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary` reports 100% line and branch
      coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
