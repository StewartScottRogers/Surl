---
id: BL-1487
title: Decide whether a delivered copy carries the envelope recipient it was delivered for, as a Delivered-To field or otherwise
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1487 — Decide whether a delivered copy carries the envelope recipient it was delivered for, as a Delivered-To field or otherwise

## Goal

An amendment to ADR-0053 decides whether a message Surl stores carries the envelope recipient (`RCPT TO`) it was delivered
for - a `Delivered-To:` or `X-Original-To:` trace field, an event member, or nothing - so a test can tell which address a
catch-all or Bcc'd copy was for, and files the work.

## Context

- Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1836-Surl-bug-report.md` item 10 (an ask): caught
  copies carry no `Delivered-To`, so a test cannot tell which address a Bcc'd copy was for.
- Today: the SMTP server writes `Return-Path` and `Received` only (ADR-0053 decision 6;
  `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpTraceFields.cs:25-34`). ADR-0102 decision 1.3 resolves a recipient through
  addresses, aliases and `--mail-catch-all` (`MailCatchAll.cs`) and stores **one copy per owner per message** however many
  `RCPT`s name it (`Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md:173`),
  so one copy can stand for several envelope recipients. The mail store's `StoreChangedEvent`s
  (`Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.Events.cs`) name the message, not the recipient.
- Delivered-To is not a standard field (Postfix, qmail and Gmail use it; RFC 9228 registers `Delivered-To` as a trace header for
  this purpose), and `X-Original-To` is Postfix's. `Surl.Api`'s `Mail.DeliverAsync`, `surl-mcp`'s `seed_mail` and the control
  endpoint's `POST /v1/mail?to=` deliver without SMTP; say whether they add the field too.
- Weigh: RFC 9228's privacy note (a Bcc recipient is revealed to whoever reads the copy - here only that recipient's owner);
  the one-copy rule (one field per recipient that resolved to the owner, in `RCPT` order?); `--max-filesize` counting the added
  bytes as it counts the trace fields (AiHelpProse's mail store paragraph); whether it is on by default or a new option, since
  configuration only grows and defaults keep today's behaviour. Upstream curl 8.21.0 only sends mail and never reads this field.
  Decided by Claude under Stewart's delegation.

## Acceptance criteria

- [x] `ADR-0053` gains an amendment, marked "Decided by Claude under Stewart's delegation", that decides yes or no and, if yes, the
      field name and exact bytes, where it goes relative to `Return-Path` and `Received`, one or several per copy, which delivery
      paths add it, whether an option turns it on, and whether the store's event names the recipient.
- [x] The amendment names any ADR-0102 decision 1.3 text it changes and the `--aihelp` paragraph that must follow.
- [x] task-planner files the follow-up tasks (SMTP server, mail store or hosts, help text; at most two projects each), each depending on
      this task, and their IDs are in this task's Log; or, if the answer is no, the Log says why and how a test tells the copies apart.
- [x] No `.cs` file changes in this task.

## Notes

Decided yes: ADR-0053 Amendment 1 - opt-in `--mail-delivered-to` (default off keeps today's bytes), `Delivered-To: <address>`
first, one per envelope address reaching the owner in RCPT order, per-owner copies delivered as one batch (keeps Bcc hidden and
all-or-none), non-SMTP paths add it too, and the delivery event always names the recipient (`DeliveredFor`). ADR-0102 decision 1.3
bullet and the AiHelpProse mail store paragraph / glossary row follow (BL-1496, BL-1497). No `.cs` file changed.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Follow-ups filed by task-planner: BL-1498, BL-1499, BL-1500, BL-1501, BL-1502, BL-1503, BL-1504, BL-1495, BL-1496, BL-1497.
- 2026-11-20: Doing -> Done. ADR-0053 amendment 1 decides an opt-in Delivered-To field and DeliveredFor event member; follow-ups BL-1498..BL-1497 filed
