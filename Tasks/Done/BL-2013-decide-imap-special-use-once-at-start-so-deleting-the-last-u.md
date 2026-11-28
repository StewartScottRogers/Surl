---
id: BL-2013
title: Decide IMAP SPECIAL-USE once at start, so deleting the last use does not withdraw it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests, Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.cs, Protocols/Mail/Surl.MailStore.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md, Documentation/Planning/Decisions/ADR-0192-the-mail-store-index-keeps-folder-uses-and-a-configured-use-wins-at-start.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2013 — Decide IMAP SPECIAL-USE once at start, so deleting the last use does not withdraw it

## Goal

The IMAP server offers `SPECIAL-USE CREATE-SPECIAL-USE`, or not, for its whole life, by what the mail store held when
the server was constructed, as ADR-0055 Amendment 2 decides.

## Context

Filed from BL-1975 (2026-11-27), which decided this in ADR-0055 Amendment 2; read it first.

Today `ImapSession.Capabilities` (`ImapSession.cs`) and `CreateMailboxWithUses` (`ImapSession.Changes.cs`) ask
`MailboxStore.HoldsAnyFolderUse` each time. So after the last mailbox with a use is deleted, the capabilities drop
the two words and `CREATE Sent (USE (\Sent))` is `NO [USEATTR] The use cannot be given to this mailbox`: a client
under test cannot delete and recreate its Sent folder, as it can on a server that has the extension.

`ImapProtocolServer`'s constructor is where to read the property once: `Surl.Composition` constructs the server after
the store is made or loaded and before a listener accepts. Both the capability list and the `CREATE` check use that one
value. Statements that describe the per-command behaviour and change with it: the XML comment on
`MailboxStore.HoldsAnyFolderUse` ("IMAP advertises `SPECIAL-USE` only while this is true"), the IMAP library's
`CLAUDE.md` ("While the store holds a folder use"), and ADR-0055 Amendment 1 point 3's note and Amendment 2's
"The code today" paragraph.

No store, event or fault changes. Upstream curl 8.21.0 sends and reads the same bytes either way.

## Acceptance criteria

- [x] A test in `Surl.Protocol.Imap.UnitTests`: after `DELETE` of the only mailbox holding a use, `CAPABILITY` on the same session and on a new session of the same server still lists `SPECIAL-USE CREATE-SPECIAL-USE`.
- [x] A test: after that `DELETE`, `CREATE Sent (USE (\Sent))` answers `OK CREATE completed` and `LIST` shows `Sent` with `\Sent`.
- [x] A test: a server constructed over a store holding no use does not offer the capabilities and answers `CREATE` with `USE` `NO [USEATTR]`, even if a mailbox of the store is given a use after construction.
- [x] The `HoldsAnyFolderUse` XML comment, the IMAP and mail store `CLAUDE.md` files and ADR-0055 (point 3's note and Amendment 2) describe the new behaviour as current.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `ImapProtocolServer`'s constructor reads `MailboxStore.HoldsAnyFolderUse` once and passes the value to each
  `ImapSession`. No new decision: ADR-0055 Amendment 2 already made it.
- The three new tests in `ImapSpecialUseTests` failed before the change and pass after it.
- Pipeline: the plan, tests and change were made in the session, because the task file already named the seam and the
  change is a few lines; `code-reviewer` reviewed it. No conformance stage: no response text changes and upstream
  curl 8.21.0 uses neither capability (ADR-0055 Amendment 1, "Measured"). No `--aihelp` fact described the
  per-command behaviour.
- Added ADR-0192 to `touches`: its Context paragraph stated the per-command behaviour in the present tense (found in
  review). No task in `Doing` named it or its folder.
- `verify`: build 0 warnings, format clean, fast tests 30425 passed, 0 failed, 3 skipped.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. IMAP decides SPECIAL-USE once at start, so deleting the last use no longer withdraws it
