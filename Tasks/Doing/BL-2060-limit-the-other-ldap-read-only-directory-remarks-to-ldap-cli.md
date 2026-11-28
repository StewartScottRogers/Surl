---
id: BL-2060
title: Limit the other LDAP read-only directory remarks to LDAP clients
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapProtocolServer.cs, Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2060 — Limit the other LDAP read-only directory remarks to LDAP clients

## Goal

The `LdapProtocolServer` XML summary and the LDAP library's `CLAUDE.md` opening description say the directory is read-only to LDAP clients, not read-only altogether.

## Context

- Found while working BL-2035 (2026-11-28), which fixed the same wording on `LdapDirectoryFile`.
- `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapProtocolServer.cs`, class `<summary>` (about line 8): "from a read-only directory".
- `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md`, opening description (about line 9): "from a read-only directory it serves".
- A host can change the in-memory directory through `LdapProtocolServer.SeedDirectoryEntries`, `ResetDirectory` and `RestoreDirectory` (ADR-0158).

## Acceptance criteria

- [ ] The `LdapProtocolServer` class summary says LDAP clients cannot change the directory.
- [ ] The opening description in the library's `CLAUDE.md` says LDAP clients cannot change the directory.
- [ ] Only the XML comment and that Markdown description change.
- [ ] `dotnet build Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
