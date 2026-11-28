---
id: BL-2078
title: Say in the LdapProtocolServer XML docs when an empty directory answers noSuchObject and that fault rules change answers
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2077]
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapProtocolServer.cs]
requirement: none
created: 2026-11-28
completed:
---
# BL-2078 — Say in the LdapProtocolServer XML docs when an empty directory answers noSuchObject and that fault rules change answers

## Goal

The `LdapProtocolServer` constructor summary and "Operations" remarks state only what is true for a seeded directory, an unbound client and a matching fault rule.

## Context

- Found while working BL-2060 (2026-11-28), by reading the code and comments.
- The public constructor summary says "every search but the root DSE's answers `noSuchObject`". The host can seed the directory (`SeedDirectoryEntries`, ADR-0158), and an unbound client without `--allow-anonymous` gets `insufficientAccessRights` (50).
- The "Operations" remarks do not say a matching fault rule can answer a request with another result code or drop the connection (`LdapSession`, including `AnswerUnrecognizedAsync` for writes).
- BL-2077 changes how a write with a critical control is answered, which these remarks describe.

## Acceptance criteria

- [ ] The constructor summary limits the `noSuchObject` statement to a directory the host has not seeded and a connection allowed to search.
- [ ] The "Operations" remarks say a matching fault rule can change the result code or drop the connection.
- [ ] Only XML comments change.
- [ ] `dotnet build Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-28: Created.
