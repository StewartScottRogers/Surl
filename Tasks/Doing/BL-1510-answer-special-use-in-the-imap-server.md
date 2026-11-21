---
id: BL-1510
title: Answer SPECIAL-USE in the IMAP server
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-1507]
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1510 — Answer SPECIAL-USE in the IMAP server

## Goal

The IMAP server advertises SPECIAL-USE only when a use is configured, marks `LIST` and `LSUB` lines with it exactly as pinned upstream curl was measured to print them, and accepts `CREATE ... (USE (...))`.

## Context

ADR-0055 Amendment 1, decisions 3-5. Measurements: `Protocols/Mail/Surl.Protocol.Imap.UnitTests/Fixtures/README.md`, section "SPECIAL-USE" (BL-1485, pinned curl 8.21.0; folders `special-use-list`, `special-use-list-return`, `special-use-list-select`, `special-use-append-sent`). Depends on the store's use (BL-1507). A use never comes from a folder's name. `LIST-EXTENDED` stays unadvertised, so `RETURN (SPECIAL-USE)` and `LIST (SPECIAL-USE)` are answered as any unimplemented `LIST` form. Pipeline `protocol` (IMAP).

## Acceptance criteria

- [ ] `CAPABILITY` gains `SPECIAL-USE CREATE-SPECIAL-USE` after `MOVE` and before `APPENDLIMIT=`, only when the store has a use configured; with none, the line is byte-identical to today's.
- [ ] `LIST "" *` and `LSUB` write attributes such as `(\HasNoChildren \Sent)` after the children attribute for a mailbox with a use, matching the BL-1485 `special-use-list` fixture; `\Noselect` placeholders never carry one.
- [ ] A replay test of the four BL-1485 SPECIAL-USE fixtures completes against the server.
- [ ] `CREATE x (USE (\Sent))` succeeds when advertised; a use already held, an unknown attribute, or any `USE` when not advertised answers `NO [USEATTR] ...`.
- [ ] `RENAME` carries the use and `DELETE` ends it (tested through the server).
- [ ] Tests pass with 100% coverage, none needing `TestCategory=Integration`, and `dotnet build` of both projects with `-warnaserror` is clean.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
