---
id: BL-1486
title: Decide whether and how the IMAP server marks folders with RFC 6154 SPECIAL-USE attributes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1485]
touches: [Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1486 — Decide whether and how the IMAP server marks folders with RFC 6154 SPECIAL-USE attributes

## Goal

An amendment to ADR-0055 decides, from BL-1485's measurements, whether the IMAP server advertises `SPECIAL-USE` (RFC 6154)
and marks folders with `\Sent`, `\Drafts`, `\Trash`, `\Junk`, `\Archive`, `\All` or `\Flagged`, how a folder gets its
attribute, and files the work.

## Context

- Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1836-Surl-bug-report.md` item 9 (an ask).
- BL-1485's fixtures and `Protocols/Mail/Surl.Protocol.Imap.UnitTests/Fixtures/README.md` say whether upstream curl 8.21.0 uses the
  attributes; whatever they say, curl must still complete every exchange ADR-0055 pins (ADR-0003).
- Stand-in value: a client under test that looks for its Sent folder by `\Sent` cannot use Surl without it. Where the attribute
  would come from: `--mailbox ...;folders=<name>,...` and `--mailbox-file` (ADR-0102 decision 1.4, `folders=`), `--shared-folder`,
  `CREATE` with RFC 6154 section 3's `USE` option, and the mail store's mailboxes (`Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxSettings.cs`,
  `StoredMailbox.cs`), which keep no such attribute today. Configuration only grows: a new key or a name-based default must keep
  today's `LIST` bytes for a start that does not ask for it, or say why it is safe to change them.
- Weigh: implicit by name (a folder named `Sent` is `\Sent`) against explicit (`folders=Sent:\Sent` or a `uses=` key); whether
  `LIST` shows attributes always or only for `RETURN (SPECIAL-USE)`; `CREATE ... (USE (\Sent))`; what `Surl.Api`, `surl-mcp`
  and the control endpoint seed and show; whether snapshot and restore keep the attribute (ADR-0131). Decided by Claude under
  Stewart's delegation.

## Acceptance criteria

- [x] `ADR-0055` gains an amendment, marked "Decided by Claude under Stewart's delegation", that decides yes or no, cites BL-1485's
      fixture folders, and if yes states the capability, the `LIST` bytes for each case BL-1485 recorded, the configuration key or
      rule, and how a test seeds, resets and reads the attribute.
- [x] If the amendment adds a configuration key, it says which ADR-0101 table row grows and the exact syntax.
- [x] task-planner files the follow-up tasks (mail store, IMAP server, configuration, help text; one kind of work and at most two
      projects each), each depending on this task, and their IDs are in this task's Log; or, if the answer is no, the Log says why.
- [x] No `.cs` file changes in this task.

## Notes

- Decided yes, explicit (no name-based default), key `folders=<name>=<use>`, capabilities only when configured. ADR-0055 Amendment 1. Follow-ups: BL-1507 to BL-1514 (BL-1506 was a throwaway, Deferred). No `.cs` changed.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. ADR-0055 Amendment 1 decides SPECIAL-USE: explicit folders=<name>=<use>; follow-ups BL-1507..BL-1514 filed
- 2026-11-20: Follow-up tasks filed: BL-1507, BL-1508, BL-1509, BL-1510, BL-1511, BL-1512, BL-1513, BL-1514.
