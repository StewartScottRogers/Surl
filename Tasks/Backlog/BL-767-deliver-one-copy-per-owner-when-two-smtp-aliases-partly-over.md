---
id: BL-767
title: Deliver one copy per owner when two SMTP aliases partly overlap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-675, BL-676]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests, Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-767 — Deliver one copy per owner when two SMTP aliases partly overlap

## Goal

When one SMTP message's recipients include two aliases that each name an owner the other does not and share
one (`team` -> ann, bob; `pair` -> bob, cat), every owner receives exactly one copy (ADR-0102 decision 1.3).

## Context

- BL-676 made `SmtpMailTransaction` (`Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary`) drop a recipient whose
  owners were all reached already and replace kept recipients whose owners a new one covers. It cannot split a
  `MailRecipient` - its constructor and `Owners` are internal to `Surl.MailStore.UnitLibrary` - so two partly
  overlapping aliases still give the shared owner two copies
  (`SmtpMailDomainTests.ServeAsync_AliasesThatEachNameAnotherOwner_KeepEveryOwnersCopy` pins only that no copy
  is lost).
- The fix needs a store-side way to deliver once per owner: e.g. a public `MailRecipient` merge
  (`MailRecipient.Union` / a `MailboxStore` method building one recipient from several) that keeps the anonymous
  owner's once-per-recipient rule (ADR-0053 decision 4), and the SMTP transaction using it.
- Waits for BL-675, which holds `Surl.MailStore.UnitLibrary`.

## Acceptance criteria

- [ ] A named test in `Surl.Protocol.Smtp.UnitTests` sends `RCPT`s to two partly overlapping aliases and finds
      exactly one copy in each owner's `INBOX`; the BL-676 test above asserts `HasCount(1, ...)` for bob.
- [ ] Under `--allow-anonymous`, each recipient still gives the anonymous owner one copy (existing tests pass
      unchanged).
- [ ] `dotnet build -warnaserror` clean, fast tests green, both libraries at 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
