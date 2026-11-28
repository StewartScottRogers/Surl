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
completed: 2026-11-28
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

- [x] The `LdapProtocolServer` class summary says LDAP clients cannot change the directory.
- [x] The opening description in the library's `CLAUDE.md` says LDAP clients cannot change the directory.
- [x] Only the XML comment and that Markdown description change.
- [x] `dotnet build Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary -warnaserror` is clean.

## Notes

- Both places also name the host's way to change the directory (`SeedDirectoryEntries`, `ResetDirectory`, `RestoreDirectory`, in memory only, ADR-0158), so "clients cannot" is not read as "nothing can".
- Verified 2026-11-28: solution build 0 warnings, `dotnet format --verify-no-changes` passed, fast tests 0 failed.
- Follow-ups filed: BL-2077 (write with a critical control is not answered 12, as ADR-0072 decision 3 says), BL-2078 (constructor summary and "Operations" remarks).

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. LdapProtocolServer's summary and the LDAP library's CLAUDE.md now say LDAP clients cannot change the directory
