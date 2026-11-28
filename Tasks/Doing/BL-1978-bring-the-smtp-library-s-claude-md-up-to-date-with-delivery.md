---
id: BL-1978
title: Bring the SMTP library's CLAUDE.md up to date with delivery, command faults and redaction
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1978 — Bring the SMTP library's CLAUDE.md up to date with delivery, command faults and redaction

## Goal

`Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/CLAUDE.md` names the methods and types the SMTP server uses today.

## Context

Found while working BL-1957 (2026-11-27), each checked against the code that day. Prose only; no behaviour, store,
event or fault changes. Code files are in the same folder as the `CLAUDE.md`.

1. The "Mail" bullet says `MailboxStore.Deliver(..., PendingMessage)` stores the message. `SmtpSession.DeliverAsync`
   calls `MailboxStore.DeliverAsync`.
2. The same bullet omits `--mail-delivered-to`: with `MailboxStore.DeliveredToEnabled` and at least one deliverable
   recipient, each element of `SmtpMailTransaction.Deliverable` (an alias element can name several owners) has its
   own pending message with `Delivered-To` lines for the paths that reach it ahead of `Return-Path`; the longest one
   decides `--max-filesize`, and they are delivered together through the `DeliverAsync` overload that takes
   `MailOwnerCopy` values (`SmtpSession.ReceiveMessageAsync`, `SmtpTraceFields.Build`; ADR-0053 Amendment 1).
3. The "Command-stage faults" bullet names `CommandStageFaults.FindAndApplyAsync`. `SmtpSession.Faults.cs` calls
   `CommandStageFaults.FindAndApplyWithClientOutcomeAsync`, and when the client leaves during a `Delay` hold it
   sends nothing and ends the session (ADR-0141 amendment 3).
4. The "What it holds" list omits that `SmtpProtocolServer` also implements `IExchangeBytesRedactingServer`:
   `CreateRedactor` returns a `LineBytesRedactor` over `SmtpLineRedactionRule`, which logs the `AUTH` initial
   response and the answers to `334 ` continuations as `****`, and logs the message lines from a `354` reply to the
   ending `.` line as they arrived (ADR-0183).

## Acceptance criteria

- [ ] The "Mail" bullet names `MailboxStore.DeliverAsync`, and the file no longer says `MailboxStore.Deliver(`.
- [ ] The "Mail" bullet says when `Delivered-To` lines are written, where they go, and that each deliverable recipient then has its own pending message.
- [ ] The faults bullet names `CommandStageFaults.FindAndApplyWithClientOutcomeAsync` and says a client that leaves during a hold is sent nothing and the session ends.
- [ ] The file names `IExchangeBytesRedactingServer` and `SmtpLineRedactionRule` and says what is masked and what is logged as it arrived.
- [ ] The diff changes only that `CLAUDE.md`.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
