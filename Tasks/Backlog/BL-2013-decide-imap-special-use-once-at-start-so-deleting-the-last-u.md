---
id: BL-2013
title: Decide IMAP SPECIAL-USE once at start, so deleting the last use does not withdraw it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests, Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.cs, Protocols/Mail/Surl.MailStore.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md]
requirement: none
created: 2026-11-27
completed:
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

- [ ] A test in `Surl.Protocol.Imap.UnitTests`: after `DELETE` of the only mailbox holding a use, `CAPABILITY` on the same session and on a new session of the same server still lists `SPECIAL-USE CREATE-SPECIAL-USE`.
- [ ] A test: after that `DELETE`, `CREATE Sent (USE (\Sent))` answers `OK CREATE completed` and `LIST` shows `Sent` with `\Sent`.
- [ ] A test: a server constructed over a store holding no use does not offer the capabilities and answers `CREATE` with `USE` `NO [USEATTR]`, even if a mailbox of the store is given a use after construction.
- [ ] The `HoldsAnyFolderUse` XML comment, the IMAP and mail store `CLAUDE.md` files and ADR-0055 (point 3's note and Amendment 2) describe the new behaviour as current.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
