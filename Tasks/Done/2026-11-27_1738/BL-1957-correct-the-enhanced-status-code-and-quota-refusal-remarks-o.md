---
id: BL-1957
title: Correct the enhanced status code and quota refusal remarks on SmtpProtocolServer
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1957 — Correct the enhanced status code and quota refusal remarks on SmtpProtocolServer

## Goal

The XML remarks on `SmtpProtocolServer` state which replies carry no enhanced status code and list every mail store
refusal as the code sends them.

## Context

Found while working BL-1948 (2026-11-27), read from source. Comment text only; no behaviour change.

In `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpProtocolServer.cs`, class remarks:

- About lines 17-18: "enhanced status codes on every reply but the greeting and `EHLO`'s and `HELO`'s". At least
  `354` (`SmtpReplies.StartData`) and the `334` continuation (`SmtpReplies.Continuation`) carry none either. Check
  every line in `SmtpReplies` before rewriting the sentence.
- About lines 51-54: the store refusals listed are `452 4.3.1`, `552` and `451 4.3.0`. The quota refusal,
  `452 4.2.2 Mailbox full` (`SmtpReplies.MailboxFull`, sent by `SmtpSession`), is missing. The library's `CLAUDE.md`
  already states it.

## Acceptance criteria

- [x] The enhanced status code sentence names every reply in `SmtpReplies` that carries no enhanced status code.
- [x] The delivery paragraph says a quota refusal is answered `452 4.2.2 Mailbox full`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

- 2026-11-27: Replies without an enhanced status code, read from `SmtpReplies` and `SmtpSession`: the greeting `220`,
  `EHLO`'s `250` lines, `HELO`'s `250`, both `501 Syntax` replies to `EHLO` and `HELO`, `354` and `334`. The remarks
  also name the upstream test case's own `501 Invalid address` and `552 Message size too large`, and say a fault rule's
  reply is sent as given.
- The delivery paragraph also says an out-of-space fault rule is answered `452 4.2.2` (`MailboxStore.DeliverAsync`
  returns `QuotaExceeded` for it).
- Verified: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 0 failed.
  The diff changes only `///` lines in `SmtpProtocolServer.cs`.
- Other stale remarks found are filed as BL-1977, BL-1978 and BL-1979.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. SmtpProtocolServer remarks name every reply without an enhanced status code and the 452 4.2.2 quota refusal
