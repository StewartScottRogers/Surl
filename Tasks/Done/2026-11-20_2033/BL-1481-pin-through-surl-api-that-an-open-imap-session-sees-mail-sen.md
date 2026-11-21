---
id: BL-1481
title: Pin through Surl.Api that an open IMAP session sees mail sent over SMTP on its next NOOP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1480]
touches: [Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1481 — Pin through Surl.Api that an open IMAP session sees mail sent over SMTP on its next NOOP

## Goal

A named `Surl.Api` test proves the cross-protocol stand-in scenario "send over SMTP, watch the open IMAP session": an IMAP
session with `INBOX` selected gets `* <n> EXISTS` on its next `NOOP` after mail reaches the mailbox by SMTP and by
`server.Mail.DeliverAsync`.

## Context

- Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1836-Surl-bug-report.md` item 2. BL-1480 pins
  (and fixes, if needed) the IMAP server's side against the mail store alone; this is the end-to-end proof through the
  shared mail store a composed instance holds (ADR-0100), as root `CLAUDE.md` asks for every cross-protocol scenario.
- The model: `Application/Surl.Api.UnitTests/SurlServerTests.MailToAddedAccount.cs` (BL-1473), which serves scripted SMTP and IMAP
  connections through `ApiTestSeams` (`Application/Surl.Api.UnitTests/ApiTestSeams.cs`) and its `FakeListenerFactory`, with
  `SmtpAndImapOptions()` and `SmtpSendTo(...)`. Those connections run a whole script at once; to keep the IMAP session open
  between `SELECT` and `NOOP`, give the test project an in-memory connection that hands over its next command only when the
  test releases it (test code only; no production change).
- Stand-in: no store, event or fault change; the test reads the `StoreChangedEvent` of the delivery from the instance's event
  stream to know when to send `NOOP`, rather than waiting on time.

## Acceptance criteria

- [x] A named test in `Application/Surl.Api.UnitTests` (e.g. `SurlServerTests.ImapSeesNewMail.cs`) logs in over IMAP, selects an `INBOX`
      holding no message, sends one message over SMTP to that account, then sends `NOOP`: the IMAP answer holds `* 1 EXISTS`
      before `OK NOOP completed`, and a `FETCH 1 BODY[]` after it returns the message.
- [x] The same with `server.Mail.DeliverAsync` instead of SMTP (`* 1 EXISTS`, then `* 2 EXISTS` after a second delivery).
- [x] The test waits on the delivery's event, never on a delay, and uses the injected `TimeProvider` if it needs time.
- [x] `dotnet test Application/Surl.Api.UnitTests --filter "TestCategory!=Integration"` is green and no test needs
      `TestCategory=Integration`.

## Notes

- If BL-1480 found and fixed a defect, these tests must fail on the commit before that fix; say so in the Log.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. Two Surl.Api tests pin that an open IMAP session is told EXISTS on NOOP after SMTP and DeliverAsync delivery; BL-1480 found no defect needing a pre-fix failure check
