---
id: BL-1510
title: Answer SPECIAL-USE in the IMAP server
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-1507]
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests, Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1510 — Answer SPECIAL-USE in the IMAP server

## Goal

The IMAP server advertises SPECIAL-USE only when a use is configured, marks `LIST` and `LSUB` lines with it exactly as pinned upstream curl was measured to print them, and accepts `CREATE ... (USE (...))`.

## Context

ADR-0055 Amendment 1, decisions 3-5. Measurements: `Protocols/Mail/Surl.Protocol.Imap.UnitTests/Fixtures/README.md`, section "SPECIAL-USE" (BL-1485, pinned curl 8.21.0; folders `special-use-list`, `special-use-list-return`, `special-use-list-select`, `special-use-append-sent`). Depends on the store's use (BL-1507). A use never comes from a folder's name. `LIST-EXTENDED` stays unadvertised, so `RETURN (SPECIAL-USE)` and `LIST (SPECIAL-USE)` are answered as any unimplemented `LIST` form. Pipeline `protocol` (IMAP).

## Acceptance criteria

- [x] `CAPABILITY` gains `SPECIAL-USE CREATE-SPECIAL-USE` after `MOVE` and before `APPENDLIMIT=`, only when the store has a use configured; with none, the line is byte-identical to today's.
- [x] `LIST "" *` and `LSUB` write attributes such as `(\HasNoChildren \Sent)` after the children attribute for a mailbox with a use, matching the BL-1485 `special-use-list` fixture; `\Noselect` placeholders never carry one.
- [x] A replay test of the four BL-1485 SPECIAL-USE fixtures completes against the server.
- [x] `CREATE x (USE (\Sent))` succeeds when advertised; a use already held, an unknown attribute, or any `USE` when not advertised answers `NO [USEATTR] ...`.
- [x] `RENAME` carries the use and `DELETE` ends it (tested through the server).
- [x] Tests pass with 100% coverage, none needing `TestCategory=Integration`, and `dotnet build` of both projects with `-warnaserror` is clean.

## Notes

- Added `MailboxStore.HoldsAnyFolderUse` (MailStore added to `touches`; no Doing task touches it): the capability is asked before login, so it cannot come from a view. It is true while any mailbox holds a use, so deleting the last use withdraws SPECIAL-USE.
- `CREATE` takes one use at most (the store holds one per mailbox); two uses answer `NO [USEATTR]`. `USE ()` is a plain create.
- The replay of `special-use-list` and `special-use-append-sent` ignores line order and one UIDVALIDITY: the recorder listed Sent, Drafts, Trash where Surl lists ordinally, and issued validities its own way. The `-return` and `-select` forms are answered `BAD Invalid arguments` as unimplemented LIST forms (LIST-EXTENDED unadvertised), tested to carry on to LOGOUT.
- Not done, outside touches: `AiHelpProse`/`AiHelpExamples` text for IMAP does not yet mention SPECIAL-USE.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. IMAP advertises SPECIAL-USE when a use is held, marks LIST/LSUB, accepts CREATE (USE (...))
