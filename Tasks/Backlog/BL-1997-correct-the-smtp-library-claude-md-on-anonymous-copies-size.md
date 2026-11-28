---
id: BL-1997
title: Correct the SMTP library CLAUDE.md on anonymous copies, SIZE refusals and the test case server
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1997 — Correct the SMTP library CLAUDE.md on anonymous copies, SIZE refusals and the test case server

## Goal

Three statements in `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/CLAUDE.md` are true of the code.

## Context

Found while working BL-1978 (2026-11-27). Prose only. Code files are in the same folder.

1. The "Mail" bullet says an owner reached by several `RCPT`s gets one copy. The anonymous owner is added once for
   each `RCPT` (`SmtpMailTransaction.AddRecipient`).
2. The "Limits" bullet says `552` past `--max-filesize` then closes. That holds for a `DATA` body; a `MAIL` `SIZE=`
   over the limit is refused by `SmtpMailParameters.FindRefusal` through `AnswerMailAsync` and the session stays open.
3. The `SmtpUpstreamTestCaseServer` bullet says it is `SmtpProtocolServer` built with the internal constructor. It is
   a separate class wrapping such an instance and implements only `IConnectionProtocolServer`. Its list of what a case
   changes omits the refusals in `SmtpSession.UpstreamTestCase.cs`: a `RCPT` with no at sign, a non-ASCII `RCPT` or
   `VRFY` address without `SMTPUTF8` in `CAPA`, and a `MAIL` `SIZE=` over the advertised `SIZE`.

## Acceptance criteria

- [ ] The "Mail" bullet says the anonymous owner gets one copy for each recipient.
- [ ] The "Limits" bullet says which `552` closes the session and that an oversize `MAIL` `SIZE=` does not.
- [ ] The test case server bullet says it wraps a `SmtpProtocolServer`, implements only `IConnectionProtocolServer`, and names the three refusals.
- [ ] The diff changes only that `CLAUDE.md`.

## Notes

## Log

- 2026-11-27: Created.
