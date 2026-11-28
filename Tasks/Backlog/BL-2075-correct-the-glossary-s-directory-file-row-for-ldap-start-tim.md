---
id: BL-2075
title: Correct the Glossary's directory file row for LDAP start-time and runtime seeds
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Wiki/Glossary.md]
requirement: FR-061
created: 2026-11-28
completed:
---
# BL-2075 — Correct the Glossary's directory file row for LDAP start-time and runtime seeds

## Goal

The Glossary's "directory file" row is true of start-time and runtime LDAP seeds.

## Context

Found while working BL-2034 (2026-11-28). The row calls `directory.ldif` "the LDAP directory's only source" and says no `--directory` is an empty directory. `SurlServerOptions.DirectoryLdif`, `surl-mcp`'s `directory_ldif` (ADR-0158 decision 3), `Directory.AddEntriesAsync` and `add_ldap_entries` also fill the directory.

## Acceptance criteria

- [ ] The row names the start-time and runtime seeds as sources beside `directory.ldif`.
- [ ] The row says an empty directory results only when there is neither the file nor a seed.
- [ ] Only `Documentation/Wiki/Glossary.md` changes.

## Notes

## Log

- 2026-11-28: Created.
