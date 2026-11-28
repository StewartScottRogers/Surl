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
completed: 2026-11-28
---
# BL-2092 — Say in the ComposeLdapDirectoryFile XML docs that host seeds can fill a directory with no file

## Goal

The XML summary of `SurlServerComposition.ComposeLdapDirectoryFile` no longer says that without `--directory` the directory is empty regardless of host seeds.

## Context

Found while working BL-2075 (2026-11-28). In `Application/Surl.Composition.UnitLibrary/SurlServerComposition.cs` lines 589-590 the summary says "none without it, so the directory is empty (ADR-0072, decision 1)". The method returns no file without `--directory`, but the host can still add entries, at start (`SurlServerOptions.DirectoryLdif`, `surl-mcp`'s `start_server` `directory_ldif`) and while serving (`Surl.Api`'s `Directory.AddEntriesAsync`, the `add_ldap_entries` tool, the control endpoint's `POST /v1/directory`; ADR-0158 decision 3). LDAP clients' writes are still refused. The Glossary's "directory file" row in `Documentation/Wiki/Glossary.md` shows the wording.

Stand-in: comment only; no store, seed, reset, event or fault changes.

## Acceptance criteria

- [x] The summary says that without `--directory` no entry comes from a file, and that the directory is empty only while the host has seeded none (ADR-0158).
- [x] The diff changes only `///` lines in `SurlServerComposition.cs` (and this task's own file).
- [x] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean.

## Notes

- Made the one-comment edit in the session rather than delegating to `align-and-document`; no ADR is needed.
- Verified: `git diff` shows only `///` lines; solution build 0 warnings; `dotnet format --verify-no-changes` clean; fast tests 30810 passed, 0 failed, 3 skipped. Integration tests not run (comment only).

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. ComposeLdapDirectoryFile's XML summary now says that without --directory no entry comes from a file and the directory is empty only while the host has seeded none
