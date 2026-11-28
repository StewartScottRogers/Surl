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
completed: 2026-11-28
---
# BL-2075 — Correct the Glossary's directory file row for LDAP start-time and runtime seeds

## Goal

The Glossary's "directory file" row is true of start-time and runtime LDAP seeds.

## Context

Found while working BL-2034 (2026-11-28). The row calls `directory.ldif` "the LDAP directory's only source" and says no `--directory` is an empty directory. `SurlServerOptions.DirectoryLdif`, `surl-mcp`'s `directory_ldif` (ADR-0158 decision 3), `Directory.AddEntriesAsync` and `add_ldap_entries` also fill the directory.

## Acceptance criteria

- [x] The row names the start-time and runtime seeds as sources beside `directory.ldif`.
- [x] The row says an empty directory results only when there is neither the file nor a seed.
- [x] Only `Documentation/Wiki/Glossary.md` changes.

## Notes

- Delivered by `align-and-document`. The row also names the control endpoint's `POST /v1/directory`, a runtime seed the Context did not list, and says a reset keeps the start-time seed and drops the runtime ones (ADR-0158 decisions 3 and 4).
- "Only the Glossary changes" is read as the product diff; this task file and the follow-up task files are board changes.
- The same wrong wording elsewhere was filed, not fixed here: BL-2085 (Glossary "LDAP directory" row, Product Overview, Requirements, Roadmap, README), BL-2086 (`surl` help and manual text), BL-2087 (`Surl.Composition` XML docs), BL-2088 (LDAP library docs).
- `dotnet build`: 0 warnings, 0 errors. Fast tests: 30510 passed, 0 failed.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The Glossary's directory file row names the start-time and runtime LDAP seeds and says when the directory is empty.
