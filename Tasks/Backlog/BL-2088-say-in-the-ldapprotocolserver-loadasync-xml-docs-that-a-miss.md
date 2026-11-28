---
id: BL-2088
title: Say in the LdapProtocolServer.LoadAsync XML docs that a missing file leaves a directory the host can seed
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2078]
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary]
requirement: none
created: 2026-11-28
completed:
---
# BL-2088 — Say in the LdapProtocolServer.LoadAsync XML docs that a missing file leaves a directory the host can seed

## Goal

The XML summary of `LdapProtocolServer.LoadAsync` no longer says a missing file serves an empty directory regardless of host seeds.

## Context

Found while working BL-2075 (2026-11-28). In `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapProtocolServer.cs` lines 140-141 the summary says "A missing file serves an empty directory". A missing file loads no entries, but the host can then add some with `SeedDirectoryEntries`, which the class summary in the same file already says (ADR-0158). LDAP clients' writes are still refused. The Glossary's "directory file" row in `Documentation/Wiki/Glossary.md` shows the wording.

BL-2078 corrects the constructor summary and the "Operations" remarks in this file, not the `LoadAsync` summary; this task follows it. If BL-2078 already changed the sentence, only verify it.

The same wording is in `LdapDirectoryFile.cs` line 16 ("A missing file is an empty directory") and in the project's `CLAUDE.md` lines 34-35 and 68; correct them here too.

Stand-in: comment only; no store, seed, reset, event or fault changes.

## Acceptance criteria

- [ ] The `LoadAsync` summary says a missing file loads no entries and that the directory is empty only while the host has seeded none through `SeedDirectoryEntries`.
- [ ] `LdapDirectoryFile`'s remarks and the project's `CLAUDE.md` say a missing file, or in-memory mode, leaves the directory empty only until the host seeds it.
- [ ] The diff changes only `///` lines in `LdapProtocolServer.cs` and `LdapDirectoryFile.cs`, the project's `CLAUDE.md`, and this task's own file.
- [ ] `dotnet build Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-28: Created.
