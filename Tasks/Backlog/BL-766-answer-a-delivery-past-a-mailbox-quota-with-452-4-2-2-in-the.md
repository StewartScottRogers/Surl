---
id: BL-766
title: Answer a delivery past a mailbox quota with 452 4.2.2 in the SMTP server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-675]
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

## Log

- 2026-11-05: Created.
