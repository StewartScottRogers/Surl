---
id: BL-1977
title: Make the SMTP server classes' XML comments true of replies, delivery, faults and test cases
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1957]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpProtocolServer.cs, Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpUpstreamTestCaseServer.cs, Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpReplies.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1977 — Make the SMTP server classes' XML comments true of replies, delivery, faults and test cases

## Goal

The XML doc comments of `SmtpProtocolServer`, `SmtpReplies` and `SmtpUpstreamTestCaseServer` say what the code sends,
stores and does.

## Context

Found while working BL-1957 (2026-11-27), each checked against the code that day. XML doc comments only; no behaviour
change, so no store, event or fault is added. Files are in `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/`. BL-1957
rewrites other sentences of the same remarks, so keep what it wrote.

1. Replies. The `SmtpProtocolServer` remarks open "**Replies.** Each is fixed text, never an echo of the peer's
   bytes", and the `SmtpReplies` summary ends "None echoes a byte the peer sent". Not every line is fixed:
   `EHLO`'s `SIZE` follows `--max-filesize`, and its `STARTTLS` and `AUTH` lines follow the TLS state and the mail
   authentication policy's offer (`SmtpSession.EhloReplyLines`); a `334` carries the policy's challenge in base64
   (`SmtpReplies.Continuation`); a fault rule gives its own reply (`SmtpSession.AnswerChosenErrorAsync`). A challenge
   can carry peer bytes: when `--auth` accepts a SCRAM mechanism, the `server-first-message` holds the client's nonce
   followed by the server's (`ScramSaslExchange.AnswerClientFirst` in `Foundation/Surl.Authentication.UnitLibrary`),
   so it reaches the peer again, base64 encoded.
2. The `SmtpProtocolServer` summary's command list omits `STARTTLS`.
3. The "**Delivery.**" paragraph says the stored message is `Return-Path` and `Received`, then the body. With
   `MailboxStore.DeliveredToEnabled` (`--mail-delivered-to`) and at least one deliverable recipient, each element of
   `SmtpMailTransaction.Deliverable` (an alias element can name several owners) has its own pending message, with
   `Delivered-To` lines for the paths that reach it ahead of `Return-Path`; the longest one decides
   `--max-filesize`, and they are delivered in one `DeliverAsync` call (`SmtpSession.ReceiveMessageAsync`,
   `SmtpTraceFields.Build`; ADR-0053 Amendment 1).
4. The remarks do not describe command-stage faults (`SmtpSession.Faults.cs`; ADR-0124 decision 4, ADR-0141): every
   recognized command asks `ExchangeContext.Faults`; an `Error` rule's reply (`451 4.3.0 Local error in processing`
   when it has none) replaces the command's answer and the command is not run; a `Delay` rule holds the answer, and
   a client that leaves during the hold is sent nothing and the session ends; a `Drop` rule aborts the connection.
5. The `SmtpUpstreamTestCaseServer` remarks omit what `SmtpSession.UpstreamTestCase.cs` also changes: `HELP` answers
   with the case's data for the `EHLO` name when it has any; a `MAIL` whose `SIZE=` exceeds the `SIZE` in the case's
   `CAPA` is answered `552 Message size too large`; a `RCPT` with no at sign is answered `501 Invalid address`; and a
   `RCPT` or `VRFY` argument with a byte past ASCII is answered `501 Invalid address` unless the `CAPA` lists
   `SMTPUTF8`.

## Acceptance criteria

- [x] Neither the `SmtpProtocolServer` remarks nor the `SmtpReplies` summary says every reply is fixed text or holds no peer byte; each names the lines that vary, including that a SCRAM `334` challenge carries the client's nonce base64 encoded.
- [x] The `SmtpProtocolServer` summary's command list includes `STARTTLS`.
- [x] The delivery paragraph says when `Delivered-To` lines are written, where they go, and that each deliverable recipient then has its own pending message.
- [x] The `SmtpProtocolServer` remarks say what an `Error`, a `Delay` and a `Drop` command-stage fault rule each do, including a client that leaves during a hold.
- [x] The `SmtpUpstreamTestCaseServer` remarks name the `HELP` data and the three refusals in Context item 5, with their reply text.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are `///` comments.

## Notes

- 2026-11-27: `align-and-document` made the edits; Context items 1 to 5 matched the code. The delivery paragraph also
  says what the code does for an alias: its one pending message lists the paths that reach any of its owners.
- 2026-11-27: Nearby sentences corrected in the same comments: `VRFY`/`EXPN` answer `252` "whatever name is asked"
  (a fault rule or a test case can answer otherwise), both `ServeAsync` summaries allow for the other ways a session
  ends, and a `REPLY`'d SASL response applies only after a `REPLY`'d `AUTH`.
- 2026-11-27: Gates: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests
  30378 passed, 0 failed, 3 skipped; no changed `.cs` line other than `///`. Integration tests not run: comments only.
- 2026-11-27: Filed BL-1995 (privacy defect: with `--mail-delivered-to`, an alias that absorbs an earlier recipient
  gives one owner's address to another, against ADR-0053 Amendment 1 decision 4; found by reading the code, not
  reproduced), BL-1996, BL-1997, BL-1998 and BL-1999 (other false comments and documents). BL-1995 must also rewrite
  the delivery paragraph written here.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The XML comments of SmtpProtocolServer, SmtpReplies and SmtpUpstreamTestCaseServer match the replies, delivery, command faults and test case refusals the code has
