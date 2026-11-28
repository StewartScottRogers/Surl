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
completed:
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

- [ ] The row no longer says "intent until BL-1487's code lands".
- [ ] Each statement the row makes about `Delivered-To` and `DeliveredFor` matches the code, checked in `Protocols/Mail/` at the time of the edit.
- [ ] The diff changes only that row of `Documentation/Wiki/Glossary.md`.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
