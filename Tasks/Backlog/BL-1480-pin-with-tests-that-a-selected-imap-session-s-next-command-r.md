---
id: BL-1480
title: Pin with tests that a selected IMAP session's next command reports mail added meanwhile as an untagged EXISTS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1480 — Pin with tests that a selected IMAP session's next command reports mail added meanwhile as an untagged EXISTS

## Goal

Named tests prove that an IMAP session with `INBOX` selected is told about mail delivered to it meanwhile - by SMTP
delivery (`MailboxStore.DeliverAsync`) or another session's `APPEND` - with `* <n> EXISTS` before the tagged `OK` of its
next `NOOP`, `CHECK` or other updating command, and any defect they find is fixed.

## Context

- Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1836-Surl-bug-report.md` item 2: mail
  delivered while an IMAP session is open is not shown to that session (no `EXISTS` on `NOOP` or the next command); a new
  session sees it. Tested against Surl `9ad60d80c` with the Curl port as the client, so it is a report, not evidence
  (ADR-0003): reproduce it here first.
- Decided already: ADR-0055 decision 5 ("The session view", `Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md:259-290`)
  and its command table (`:181`, `:190`): before the tagged response of `NOOP`, `CHECK` and every command except `FETCH`,
  `STORE`, `SEARCH`, `COPY`, `MOVE` and their `UID` forms, the server sends the pending `EXPUNGE`s and `* <n> EXISTS`
  (RFC 3501 section 7.4.1; RFC 9051 section 7.5.1). So a `FETCH` as "the next command" withholds `EXISTS` by design.
- The code: `ImapSession.RespondAsync` (`Protocols/Mail/Surl.Protocol.Imap.UnitLibrary/ImapSession.cs:573-589`) reads the
  selected mailbox through the session's `MailView` and sends `ImapSelectedMailbox.Update`
  (`ImapSelectedMailbox.cs:103`, `Refresh` at `:115-129`, which adds every UID at or above the view's `nextUid`). The only
  pin today is a same-session one, `ImapMessageChangeTests.ServeAsync_MoveIntoTheSelectedMailbox_ExpungesThenReportsTheCopyAsExists`
  (`Protocols/Mail/Surl.Protocol.Imap.UnitTests/ImapMessageChangeTests.cs:137`). Nothing pins a delivery from outside the
  session. Places to suspect if a test fails: the owner a `MailView` holds after ADR-0185's run-time account handling
  (`Protocols/Mail/Surl.MailStore.UnitLibrary/MailView.cs`, `MailboxStore.ReadMailbox` at `MailboxStore.cs:703`), and
  `nextUid` when the mailbox was empty at `SELECT`.
- `Protocols/Mail/Surl.Protocol.Imap.UnitTests/ActAfterWriteConnection.cs` runs an action between two of a session's commands;
  it takes a synchronous `Action`, so give it an async overload if `DeliverAsync` needs one.
- Upstream curl 8.21.0 passes a custom command's untagged data to stdout as sent: its own test 818, "IMAP NOOP
  (CUSTOMREQUEST)" (`Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data/test818`, tag `curl-8_21_0`), replies
  `* 22 EXPUNGE`, `* 23 EXISTS`, `* 3 RECENT` to `A003 NOOP`. Each curl run is a new session, so curl itself never sees a
  pending update; this is for a stand-in test client that keeps its session open.
- Stand-in: the shared mail store (ADR-0050, ADR-0100) is what SMTP, IMAP, `Surl.Api` and the control endpoint all write; no
  new event is published (the store already publishes the delivery, ADR-0127).

## Acceptance criteria

- [ ] A named test in `Protocols/Mail/Surl.Protocol.Imap.UnitTests` selects `INBOX` holding one message, delivers a second through
      `MailboxStore.DeliverAsync` between commands, and sends `NOOP`: the answer is `* 2 EXISTS` then `<tag> OK NOOP completed`.
- [ ] The same with an empty `INBOX` at `SELECT` (`* 1 EXISTS`), and with the message appended by a second IMAP session.
- [ ] A named test shows `CHECK` and `STATUS INBOX (MESSAGES)` carry the same `* <n> EXISTS`, and one shows `FETCH 1 UID` does not
      (ADR-0055 decision 5), the next `NOOP` then sending it.
- [ ] If any of these fail, the fix is in `Surl.Protocol.Imap.UnitLibrary` (or, if the cause is in `Surl.MailStore.UnitLibrary`, stop
      and file that with task-planner rather than widening this task), and the Log names the cause and file:line.
- [ ] `dotnet build Protocols/Mail/Surl.Protocol.Imap.UnitLibrary -warnaserror` is clean, the fast tests are green and coverage stays at
      100% line and branch; no test needs `TestCategory=Integration`.

## Notes

- IMAP `IDLE` (RFC 2177) is not offered and is not part of this task; upstream curl 8.21.0 never sends it.

## Log

- 2026-11-20: Created.
