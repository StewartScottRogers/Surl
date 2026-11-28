---
id: BL-1999
title: Make the glossary's trace fields row true of the Delivered-To code that exists
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2000]
touches: [Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1999 — Make the glossary's trace fields row true of the Delivered-To code that exists

## Goal

The "trace fields" row of `Documentation/Wiki/Glossary.md` says what the code does with `--mail-delivered-to` and
`DeliveredFor`.

## Context

Found while working BL-1977 (2026-11-27). The row says the `Delivered-To` lines are "intent until BL-1487's code
lands", but `SmtpTraceFields.Build` and `SmtpSession.ReceiveMessageAsync` write them. The row also says `DeliveredFor`
always lists the envelope addresses; on 2026-11-27 the SMTP server never set it. BL-2000 fixes that, so this task
waits for it. ADR-0053 Amendment 1.

## Acceptance criteria

- [x] The row no longer says "intent until BL-1487's code lands".
- [x] Each statement the row makes about `Delivered-To` and `DeliveredFor` matches the code, checked in `Protocols/Mail/` at the time of the edit.
- [x] The diff changes only that row of `Documentation/Wiki/Glossary.md`.

## Notes

- Checked against `SmtpSession.ReceiveMessageAsync`/`DeliverAsync`, `SmtpTraceFields.Build`,
  `SmtpMailTransaction.DeliverablePerOwner`, `MailboxStore.Seed`, `MailboxStore.PublishMessage`, and for the seed
  sentence `SurlServerMail.DeliverAsync` (`Surl.Api`) and `ControlStoreRequests.SeedMailAsync` (`Surl.Control`).
- The row now also says what a seed gets with the option, because the option changes seeded bytes too and the Goal
  asks what the code does with it.
- Edited directly in the session rather than through `align-and-document`: a one-row change. No `.cs` file changed, so
  no `/verify`; `dotnet build` was clean and the fast tests passed.
- Filed BL-2049: the neighbouring "message file" row still says SMTP hands the store its whole message.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The glossary's trace fields row says what the code does with --mail-delivered-to and DeliveredFor
