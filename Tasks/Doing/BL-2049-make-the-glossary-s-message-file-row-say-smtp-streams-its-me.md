---
id: BL-2049
title: Make the glossary's message file row say SMTP streams its message into a pending file
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2049 — Make the glossary's message file row say SMTP streams its message into a pending file

## Goal

The "message file" row of `Documentation/Wiki/Glossary.md` says how SMTP and IMAP write a message file today.

## Context

Found while working BL-1999 (2026-11-27). The row says "the SMTP server hands the store its whole message, and the save
after the delivery writes the file". `SmtpSession.ReceiveMessageAsync`
(`Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.cs`) streams `DATA` into a `PendingMessage` from
`MailboxStore.CreatePendingMessage`, as the project's `CLAUDE.md` and
`Protocols/Mail/Surl.MailStore.UnitLibrary/CLAUDE.md` (BL-278) say.

## Acceptance criteria

- [ ] The row no longer says the SMTP server hands the store its whole message.
- [ ] Each statement the row makes about how SMTP and IMAP write a message file matches the code in `Protocols/Mail/` at the time of the edit.
- [ ] The diff changes only that row of `Documentation/Wiki/Glossary.md`.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
