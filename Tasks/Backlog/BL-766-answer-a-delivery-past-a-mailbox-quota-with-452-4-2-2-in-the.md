---
id: BL-766
title: Answer a delivery past a mailbox quota with 452 4.2.2 in the SMTP server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-675, BL-1035]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-766 — Answer a delivery past a mailbox quota with 452 4.2.2 in the SMTP server

## Goal

When `MailboxStore.Deliver` returns `MailStoreOutcome.QuotaExceeded` after `DATA`, the SMTP server answers
`452 4.2.2 Mailbox full` and nothing is delivered to any recipient, as ADR-0102 decision 1.4 says.

## Context

- ADR-0102 decision 1.4's table, row "SMTP, after the body": `452 4.2.2 Mailbox full` (RFC 5321 section 4.2.2,
  RFC 3463 `4.2.2`), measured as R5 (upstream curl 8.21.0 exits 8). The quota is never checked at `RCPT`.
- BL-675 added `MailStoreOutcome.QuotaExceeded` to `Surl.MailStore`; `SmtpSession`'s outcome mapping
  (`Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.cs`, the switch beside `SmtpReplies.StoreFull`) has
  no row for it yet. Found while finishing BL-675; BL-677 covers IMAP, not SMTP.
- Replay R5's request bytes from the ADR through the fake connection; measure again with `Record-CurlExchange.ps1`
  if the ADR's record falls short.

## Acceptance criteria

- [ ] A named test in `Surl.Protocol.Smtp.UnitTests` delivers past a recipient's quota and pins the reply
      `452 4.2.2 Mailbox full`, that no recipient's `INBOX` holds the message, and the session's log line.
- [ ] `RCPT TO` an over-quota mailbox is still answered as any known recipient (named test).
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 2): code and tests written but left uncommitted (the shift stashes them) because
  `dotnet` cannot run: global.json pins SDK 10.0.401, which is not installed (BL-1035). What was written:
  `SmtpReplies.MailboxFull = "452 4.2.2 Mailbox full"`; a `QuotaExceeded` row in `SmtpSession.RefuseMessage`
  noting "Message refused: a recipient's mailbox would pass its quota" (the store does not say which owner,
  so the note names none); the project CLAUDE.md line; and two tests in `SmtpMailDomainTests`
  (`ServeAsync_DeliveryPastARecipientsQuota_Answers452MailboxFullAndDeliversToNoRecipient`, R5's bytes,
  and `ServeAsync_RcptToAnOverQuotaMailbox_IsAcceptedAsAnyKnownRecipient`). No ADR needed: ADR-0102
  decision 1.4 already decides the reply. Rerun the build and fast tests once BL-1035 is Done.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing can build or test
