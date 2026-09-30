---
id: BL-272
title: Say Phase 3 in the MailStore and LineProtocol CLAUDE.md files
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Surl.MailStore.UnitLibrary/CLAUDE.md, Surl.LineProtocol.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-09-30
completed:
---
# BL-272 — Say Phase 3 in the MailStore and LineProtocol CLAUDE.md files

## Goal

`Surl.MailStore.UnitLibrary/CLAUDE.md` and `Surl.LineProtocol.UnitLibrary/CLAUDE.md` name the
phase their library belongs to as Phase 3, like the SMTP, IMAP and POP3 libraries' `CLAUDE.md`
files do.

## Context

- Found by BL-214 (documenting Phase 3 mail, 2026-09-30); outside BL-214's `touches`, so filed here.
- Both files' line 3 reads `Phase 1.` The mail store and the CRLF line machinery exist only for
  the mail servers, which are Phase 3 (`Documentation/Product/Product-Overview.md`, "Phasing" and
  "Built for Phase 3: SMTP, IMAP and POP3"; ADR-0050 decision 1 created both libraries for them).
  `Surl.Protocol.Smtp.UnitLibrary/CLAUDE.md` line 3 is the model: `Phase 3.` followed by what the
  library is.
- While there, check every other statement in the two files against the code (the
  `align-and-document` rule: every statement true of the code as it is now). In particular
  `Surl.MailStore.UnitLibrary/CLAUDE.md`'s "Streaming" paragraph says the byte overloads stay
  "for the servers not yet streaming"; leave it true of whatever SMTP does when this task runs
  (BL-271 moves SMTP to streaming; this task does not wait on it and does not change code).

## Acceptance criteria

- [ ] Neither `Surl.MailStore.UnitLibrary/CLAUDE.md` nor `Surl.LineProtocol.UnitLibrary/CLAUDE.md`
      contains `Phase 1`; each says `Phase 3.` on its third line.
- [ ] Every type, member and task each file names exists or is done as described (checked with a
      search of the library's `.cs` files and the board).
- [ ] No file outside the two in `touches` changes.

## Notes

- A `docs` task: no code changes.

## Log

- 2026-09-30: Created.
- 2026-09-30: Filed by BL-214.
- 2026-09-30: Backlog -> Doing.
