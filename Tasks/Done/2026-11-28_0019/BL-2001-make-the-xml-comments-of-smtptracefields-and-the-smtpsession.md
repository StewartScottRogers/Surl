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
completed: 2026-11-27
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

- [x] The `SmtpTraceFields` summary and `Build`'s `<returns>` say when `Delivered-To` lines are written and that they go ahead of `Return-Path`, and `<param name="deliveredTo">` comes before `<returns>`.
- [x] The `SmtpSession` class summary in `SmtpSession.cs` says when a `DATA` body goes into one pending message and when into one per owner.
- [x] The `SmtpSession.UpstreamTestCase.cs` summary names the refusal of a non-ASCII `RCPT` or `VRFY` argument, its reply `501 Invalid address` and the `SMTPUTF8` condition.
- [x] The `SmtpSession.Faults.cs` summary says what happens when the client leaves during a `Delay` hold.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are `///` comments.

## Notes

- XML doc comments only, in the four files named by `touches`; `align-and-document` made the edits and the wording
  was checked against the code.
- The session writes through `SmtpTeeStream` with one pending message as well as with several, and "one per owner"
  means one per recipient for the anonymous owner; the `SmtpSession` summary says both.
- The `SmtpSession.UpstreamTestCase.cs` summary also gives the reply text of the other two refusals and names
  upstream's `VRFY_smtp`, as the comment on `IsAddressTheCaseAccepts` does.
- Verified 2026-11-27: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests
  30572 passed, 0 failed, 3 skipped. Integration tests not run: no behaviour changed.
- Left as they are, outside the four points and too small to file: `Build`'s `receivedAt` parameter says "When the
  `354` was sent" while the clock is read just before the `354` is written. The glossary's "trace fields" row is
  BL-1999's.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The XML comments of SmtpTraceFields and the SmtpSession parts name the Delivered-To lines, the per-owner pending messages, the non-ASCII address refusal and a client leaving during a delay hold.
