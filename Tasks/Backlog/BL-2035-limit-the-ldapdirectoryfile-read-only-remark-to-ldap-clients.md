---
id: BL-2035
title: Limit the LdapDirectoryFile read-only remark to LDAP clients
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapDirectoryFile.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-2035 — Limit the LdapDirectoryFile read-only remark to LDAP clients

## Goal

The XML remark on `LdapDirectoryFile` says who cannot change the directory, and that the host can.

## Context

- Found while working BL-1987 (2026-11-27).
- `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapDirectoryFile.cs`, the class `<remarks>` (about line 12): "It is read once, at start, and never written: the directory is read-only while serving." The second clause is not limited to LDAP clients, and a host can change the in-memory directory through `LdapProtocolServer.SeedDirectoryEntries`, `ResetDirectory` and `RestoreDirectory` (ADR-0158). The file itself is never written, and a missing file is an empty directory; both stay true.

## Acceptance criteria

- [ ] The remark says LDAP clients cannot change the directory while serving, that the host can change it in memory only (ADR-0158), and still that the file is read once at start, never written, and that a missing file is an empty directory.
- [ ] Only the XML comment in that one file changes.
- [ ] `dotnet build Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
