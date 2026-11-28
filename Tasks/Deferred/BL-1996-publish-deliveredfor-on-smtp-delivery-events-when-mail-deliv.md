---
id: BL-1996
title: Publish DeliveredFor on SMTP delivery events when --mail-delivered-to is off
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1995]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1996 — Publish DeliveredFor on SMTP delivery events when --mail-delivered-to is off

## Goal

With `--mail-delivered-to` off, each SMTP delivery `StoreChangedEvent` names in `DeliveredFor` the envelope
addresses, in `RCPT` order, that resolved to the copy's owner.

## Context

Found while working BL-1978 (2026-11-27). ADR-0053 Amendment 1 decision 7 (`Documentation/Planning/Decisions/`) says
the event names the recipient whatever the option, so no new ADR is needed. `SmtpSession.DeliverAsync` passes
recipients that record no addresses to `MailboxStore.DeliverAsync`; `MailRecipient.ForAddresses` (public, in
`Surl.MailStore.UnitLibrary`) records them. BL-1995 sets the option-on lists first and works out which addresses
reached which owner. An alias recipient names several owners, and each owner's event must list only that owner's
addresses. Stored bytes, the shared message file and the replies with the option off stay as they are.

## Acceptance criteria

- [ ] A new test in `Surl.Protocol.Smtp.UnitTests`, seen to fail before the fix (said in the Log), delivers with the option off to two accounts and asserts each delivery event's `DeliveredFor` holds exactly that account's address.
- [ ] A new test with the option off, `RCPT team@` then `pair@` (the aliases of `SmtpDeliveredToTests`), asserts ann's event lists `team@`, bob's `team@` and `pair@`, and cat's `pair@`.
- [ ] `ServeAsync_OptionOff_StoresTheBytesItAlwaysDid` passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the changed code has 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Deferred. Duplicate of BL-2000, which sets DeliveredFor with the option off since 2026-11-27; look again only if the further tests its criteria name are wanted.
