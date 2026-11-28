---
id: BL-1997
title: Name the three upstream test case refusals in the SMTP library's CLAUDE.md
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1978]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1997 — Name the three upstream test case refusals in the SMTP library's CLAUDE.md

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

- [ ] The bullet says a `MAIL` whose `SIZE=` exceeds the case's `CAPA` `SIZE` is answered `552 Message size too large`.
- [ ] The bullet says a `RCPT` with no at sign is answered `501 Invalid address`.
- [ ] The bullet says a `RCPT` or `VRFY` argument with a byte past ASCII is answered `501 Invalid address` unless the `CAPA` lists `SMTPUTF8`.
- [ ] The diff changes only that `CLAUDE.md`.

## Notes

## Log

- 2026-11-27: Created.
