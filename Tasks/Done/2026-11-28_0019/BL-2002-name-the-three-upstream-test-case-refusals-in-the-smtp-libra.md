---
id: BL-2002
title: Name the three upstream test case refusals in the SMTP library's CLAUDE.md
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1978]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2002 — Name the three upstream test case refusals in the SMTP library's CLAUDE.md

## Goal

The `SmtpUpstreamTestCaseServer` bullet of `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/CLAUDE.md` names the three
refusals a test case's session makes.

## Context

Found while working BL-1977 (2026-11-27), checked against the code that day. Prose only; no behaviour, store, event
or fault changes. BL-1978 edits other bullets of the same file and does not cover these; keep what it wrote.

`SmtpSession.UpstreamTestCase.cs`, in the same folder, refuses as upstream's test server does (the code names
`ftpserver.pl` at `curl-8_21_0`):

1. A `MAIL` whose `SIZE=` exceeds the `SIZE` in the case's `CAPA` is answered `552 Message size too large`
   (`AnswerUpstreamTestCaseMailAsync`).
2. A `RCPT` with no at sign is answered `501 Invalid address` (`HasAtSign`).
3. A `RCPT` or `VRFY` argument with a byte past ASCII is answered `501 Invalid address` unless the case's `CAPA`
   lists `SMTPUTF8` (`IsAddressTheCaseAccepts`).

## Acceptance criteria

- [x] The bullet says a `MAIL` whose `SIZE=` exceeds the case's `CAPA` `SIZE` is answered `552 Message size too large`.
- [x] The bullet says a `RCPT` with no at sign is answered `501 Invalid address`.
- [x] The bullet says a `RCPT` or `VRFY` argument with a byte past ASCII is answered `501 Invalid address` unless the `CAPA` lists `SMTPUTF8`.
- [x] The diff changes only that `CLAUDE.md`.

## Notes

- 2026-11-27: Commit `393a7afbd` (after this task was filed) had already added the sentence to the
  `SmtpUpstreamTestCaseServer` bullet. It changes only that `CLAUDE.md`. This run made no further edit:
  a second wording of the same three facts would add nothing. The fourth box is met by that commit;
  this run changes only this task file.
- Checked the sentence against `SmtpSession.UpstreamTestCase.cs` (`AnswerUpstreamTestCaseMailAsync`,
  `HasAtSign`, `IsAddressTheCaseAccepts`) and `ReplyAsync` (the session goes on). All three match.
- `align-and-document` was not delegated, since there was nothing left to write. `dotnet build` was
  clean (0 warnings) and the fast tests exited 0.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The SMTP library's CLAUDE.md names the three upstream test case refusals (added by 393a7afbd, checked against the code)
