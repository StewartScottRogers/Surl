---
id: BL-1473
title: Pin with tests that SMTP mail to an account added at run time is delivered to its mailbox, and refuse it at RCPT TO if it is not
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Api.UnitTests, Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1473 — Pin with tests that SMTP mail to an account added at run time is delivered to its mailbox, and refuse it at RCPT TO if it is not

## Goal

Mail sent over SMTP to an account added while the server runs (`add_account`, `Accounts.AddAsync`) lands in that
account's INBOX, `Mail.DeliverAsync` to it succeeds, and a test pins both. If either still fails, the cause is fixed.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1609-Surl-bug-report.md` item 2, tested at
  `9ad60d80c`: SMTP to a run-time-added account answered `250` and the mail was discarded; `Mail.DeliverAsync` to
  the same account threw `NoSuchAccount`.
- `9ad60d80c` is the commit that claimed BL-1450. BL-1450 (decision), BL-1451 (mail-store owners added and retired
  at run time) and BL-1452 (`fcc2b9844`, "give an added account its mailboxes and retire a removed one's") landed
  after it, so this is probably fixed on `master`. Nothing pins the SMTP path end to end yet.
- Secure by default: a recipient that cannot be delivered should be refused at `RCPT TO`, never accepted and
  discarded, unless `--mail-domain` rules say to accept and discard (`--aihelp mail`).

## Acceptance criteria

- [x] A named fast test (composition or `Surl.Api`) starts SMTP and IMAP, adds an account at run time, sends a
      message over SMTP to it, and reads it from that account's INBOX over IMAP.
- [x] A named test calls `Mail.DeliverAsync` to a run-time-added account and reads the message back.
- [x] A named test removes the account, then sends to it over SMTP, and pins the answer the BL-1450 decision gives
      (refused at `RCPT TO` with the reply code it names, or accepted and discarded only where `--mail-domain`
      says so).
- [x] If any of these fails on `master`, the cause is fixed and named in the Log with file:line.
- [x] `dotnet build` clean, fast tests green, coverage gates held.

## Notes

- Already fixed on master (BL-1451, BL-1452): all three paths pass with no production change. Tests: `SurlServerTests.MailToAddedAccount.cs` in `Surl.Api.UnitTests` (touches: the Api tests project already named).
- Decision (follows ADR-0185 decision 1 and ADR-0102, no recipient enumeration): mail to a removed account is accepted at `RCPT TO` (250 2.1.5) and discarded, as for any unknown recipient in a served domain; only a recipient outside every `--mail-domain` is refused `550 5.7.1 Relaying denied`. `Mail.DeliverAsync` to a removed account does throw `SurlSeedRefusedException`. Both pinned. No new ADR needed.
- Tests authenticate over SMTP (AUTH PLAIN as alice): `--allow-anonymous` sends all mail to one anonymous owner and masks routing.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. Pinned by four Api tests; already fixed on master by BL-1451/BL-1452
