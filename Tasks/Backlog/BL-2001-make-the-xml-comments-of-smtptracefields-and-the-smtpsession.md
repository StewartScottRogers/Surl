---
id: BL-2001
title: Make the XML comments of SmtpTraceFields and the SmtpSession parts name everything the code does
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2000]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpTraceFields.cs, Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.cs, Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.UpstreamTestCase.cs, Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.Faults.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-2001 — Make the XML comments of SmtpTraceFields and the SmtpSession parts name everything the code does

## Goal

The XML doc comments in `SmtpTraceFields.cs`, `SmtpSession.cs`, `SmtpSession.UpstreamTestCase.cs` and
`SmtpSession.Faults.cs` name everything the points below say the code does.

## Context

Found while working BL-1977 (2026-11-27), each checked against the code that day. XML doc comments only; no
behaviour change, so no store, event or fault is added. Files are in `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/`.
BL-2000 changes how pending messages are made, so describe the code as it is after BL-2000.

1. `SmtpTraceFields.cs`: the class summary says "The two trace fields" and `Build`'s `<returns>` says "The two
   lines", but `Build` also writes one `Delivered-To` line per `deliveredTo` address ahead of `Return-Path` when the
   list is given (ADR-0053 Amendment 1). Its `<param name="deliveredTo">` sits after `<returns>`.
2. `SmtpSession.cs`: the class summary says a `DATA` body streams into one `PendingMessage`. With
   `MailboxStore.DeliveredToEnabled` and a recipient to deliver to there are several (BL-2000: one per owner),
   written through `SmtpTeeStream`.
3. `SmtpSession.UpstreamTestCase.cs`: the summary omits that `IsAddressTheCaseAccepts` answers a `RCPT` or `VRFY`
   argument with a byte past ASCII `501 Invalid address` unless the case's `CAPA` lists `SMTPUTF8`.
4. `SmtpSession.Faults.cs`: the class summary omits that a client that leaves during a `Delay` rule's hold is sent
   nothing, the command is not run and the session ends (`AnswerUnlessFaultedAsync`; ADR-0141 amendment 3).

## Acceptance criteria

- [ ] The `SmtpTraceFields` summary and `Build`'s `<returns>` say when `Delivered-To` lines are written and that they go ahead of `Return-Path`, and `<param name="deliveredTo">` comes before `<returns>`.
- [ ] The `SmtpSession` class summary in `SmtpSession.cs` says when a `DATA` body goes into one pending message and when into one per owner.
- [ ] The `SmtpSession.UpstreamTestCase.cs` summary names the refusal of a non-ASCII `RCPT` or `VRFY` argument, its reply `501 Invalid address` and the `SMTPUTF8` condition.
- [ ] The `SmtpSession.Faults.cs` summary says what happens when the client leaves during a `Delay` hold.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are `///` comments.

## Notes

## Log

- 2026-11-27: Created.
