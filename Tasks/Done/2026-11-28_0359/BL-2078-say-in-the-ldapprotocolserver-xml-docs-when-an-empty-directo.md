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
completed: 2026-11-28
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

- [x] The constructor summary limits the `noSuchObject` statement to a directory the host has not seeded and a connection allowed to search.
- [x] The "Operations" remarks say a matching fault rule can change the result code or drop the connection.
- [x] Only XML comments change.
- [x] `dotnet build Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary -warnaserror` is clean.

## Notes

- `align-and-document` wrote the comments; its separate "Fault rules" paragraph was then merged into
  "Operations", where the second criterion asks for the statement.
- The constructor summary also excludes a base that is not an RFC 4514 DN (`invalidDNSyntax`), and a
  critical control or an `error` or `drop` fault rule that comes first.
- "Operations" now says abandon and unbind ignore a critical control, and that writes need no bind.
- No ADR: comments only, describing what the code does.
- Verified: full `dotnet build` 0 warnings, 0 errors; fast tests passed; `dotnet format
  --verify-no-changes` on the file clean.
- Follow-ups: the `LdapMessageDecoder` remarks omitting `CompareRequest` are already BL-2081; the
  spaces-only search base is filed as BL-2089.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The LdapProtocolServer XML docs say when an empty directory answers noSuchObject, that fault rules change answers, and which requests ignore a critical control
