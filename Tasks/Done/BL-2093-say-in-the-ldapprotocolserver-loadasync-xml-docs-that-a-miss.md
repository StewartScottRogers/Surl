---
id: BL-2093
title: Say in the LdapProtocolServer.LoadAsync XML docs that a missing file leaves a directory the host can seed
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2078]
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2093 — Say in the LdapProtocolServer.LoadAsync XML docs that a missing file leaves a directory the host can seed

## Goal

The XML summary of `LdapProtocolServer.LoadAsync` no longer says a missing file serves an empty directory regardless of host seeds.

## Context

Found while working BL-2075 (2026-11-28). In `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapProtocolServer.cs` lines 140-141 the summary says "A missing file serves an empty directory". A missing file loads no entries, but the host can then add some with `SeedDirectoryEntries`, which the class summary in the same file already says (ADR-0158). LDAP clients' writes are still refused. The Glossary's "directory file" row in `Documentation/Wiki/Glossary.md` shows the wording.

BL-2078 corrects the constructor summary and the "Operations" remarks in this file, not the `LoadAsync` summary; this task follows it. If BL-2078 already changed the sentence, only verify it.

The same wording is in `LdapDirectoryFile.cs` line 16 ("A missing file is an empty directory") and in the project's `CLAUDE.md` lines 34-35 and 68; correct them here too.

Stand-in: comment only; no store, seed, reset, event or fault changes.

## Acceptance criteria

- [x] The `LoadAsync` summary says a missing file loads no entries and that the directory is empty only while the host has seeded none through `SeedDirectoryEntries`.
- [x] `LdapDirectoryFile`'s remarks and the project's `CLAUDE.md` say a missing file, or in-memory mode, leaves the directory empty only until the host seeds it.
- [x] The diff changes only `///` lines in `LdapProtocolServer.cs` and `LdapDirectoryFile.cs`, the project's `CLAUDE.md`, and this task's own file.
- [x] `dotnet build Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary -warnaserror` is clean.

## Notes

Made the comment and `CLAUDE.md` edits directly rather than through `align-and-document`: three sentences, already read in full, and a subagent would cost more than the change.

BL-2078 had not changed the `LoadAsync` sentence, so it is changed here. The summary says "empty only while the host has seeded none", matching the Glossary's "directory file" row. `LdapDirectoryFile`'s internal `LoadAsync` and `ReadAsync` summaries describe the directory at the moment of reading, which is true, and are unchanged.

Verify: solution build with `-warnaserror` 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests all passed (Ldap 602). No Integration run: comments and prose only.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The LDAP XML docs and project CLAUDE.md now say a missing directory file loads no entries and the host can seed the directory
