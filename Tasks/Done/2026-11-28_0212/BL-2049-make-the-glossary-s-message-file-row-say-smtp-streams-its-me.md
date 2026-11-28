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
completed: 2026-11-28
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

- [x] The row no longer says the SMTP server hands the store its whole message.
- [x] Each statement the row makes about how SMTP and IMAP write a message file matches the code in `Protocols/Mail/` at the time of the edit.
- [x] The diff changes only that row of `Documentation/Wiki/Glossary.md`.

## Notes

- The row now says SMTP `DATA` and IMAP `APPEND` each stream into `messages/.pending-<guid>`, renamed to `<n>` when the
  store keeps the message and deleted when it does not; the save afterwards writes only the index; SMTP's stream is the
  trace fields then the unstuffed body, one pending and message file per copy under `--mail-delivered-to`; without a data
  directory both are held in memory.
- Checked against `SmtpSession.ReceiveMessageAsync` and `DeliverAsync`, `ImapSession.Changes.cs`, `PendingMessage.cs`,
  `MailStoreFiles.cs` and `MailboxStore.cs`. `dotnet build` 0 warnings; fast tests all passed.
- Follow-ups filed: BL-2066 (SMTP "Message stored" note against ADR-0053), BL-2067 (ADR-0050 decision 7 against the
  code on index writes and failed saves).

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The glossary's message file row says SMTP DATA and IMAP APPEND stream into a pending file and the save writes only the index
