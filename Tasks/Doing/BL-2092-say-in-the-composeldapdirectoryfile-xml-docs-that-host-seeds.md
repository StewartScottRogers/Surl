---
id: BL-2092
title: Say in the ComposeLdapDirectoryFile XML docs that host seeds can fill a directory with no file
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary/SurlServerComposition.cs]
requirement: none
created: 2026-11-28
completed:
---
# BL-2092 — Say in the ComposeLdapDirectoryFile XML docs that host seeds can fill a directory with no file

## Goal

The XML summary of `SurlServerComposition.ComposeLdapDirectoryFile` no longer says that without `--directory` the directory is empty regardless of host seeds.

## Context

Found while working BL-2075 (2026-11-28). In `Application/Surl.Composition.UnitLibrary/SurlServerComposition.cs` lines 589-590 the summary says "none without it, so the directory is empty (ADR-0072, decision 1)". The method returns no file without `--directory`, but the host can still add entries, at start (`SurlServerOptions.DirectoryLdif`, `surl-mcp`'s `start_server` `directory_ldif`) and while serving (`Surl.Api`'s `Directory.AddEntriesAsync`, the `add_ldap_entries` tool, the control endpoint's `POST /v1/directory`; ADR-0158 decision 3). LDAP clients' writes are still refused. The Glossary's "directory file" row in `Documentation/Wiki/Glossary.md` shows the wording.

Stand-in: comment only; no store, seed, reset, event or fault changes.

## Acceptance criteria

- [ ] The summary says that without `--directory` no entry comes from a file, and that the directory is empty only while the host has seeded none (ADR-0158).
- [ ] The diff changes only `///` lines in `SurlServerComposition.cs` (and this task's own file).
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
