---
id: BL-2090
title: Say in the Glossary, Product Overview, Requirements and Roadmap that the host can change the LDAP directory
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2075]
touches: [Documentation/Wiki/Glossary.md, Documentation/Product/Product-Overview.md, Documentation/Product/Requirements.md, Documentation/Planning/Roadmap.md, README.md]
requirement: FR-049
created: 2026-11-28
completed: 2026-11-28
---
# BL-2090 — Say in the Glossary, Product Overview, Requirements and Roadmap that the host can change the LDAP directory

## Goal

The four documents' statements about the LDAP directory being read-only or empty are true when the host seeds, resets or restores it (ADR-0158).

## Context

Found while working BL-2075 (2026-11-28), by reading the documents against the code. The statements:

- `Documentation/Wiki/Glossary.md`, the "LDAP directory" row (line 243): "Read-only while serving".
- `Documentation/Product/Product-Overview.md`, "The LDAP directory" bullet (lines 399-400): "a missing file, and in-memory mode, is an empty directory. The directory is read-only".
- `Documentation/Product/Requirements.md`, FR-049 (line 86): "a read-only directory read once at start ... empty without it or when the file is missing".
- `Documentation/Planning/Roadmap.md`, Milestone 5 "Delivers" (lines 121-122): "a read-only directory loaded from `<path>/.surl/ldap/directory.ldif` (empty in memory)".
- `README.md` lines 353-355: "from a read-only directory ... without it, or without the file, an empty one".

What is true: LDAP clients' add, delete, modify and rename are still refused `unwillingToPerform`, and `directory.ldif` is never written. The host adds entries in memory, at start (`SurlServerOptions.DirectoryLdif`, `surl-mcp`'s `start_server` `directory_ldif`) and while serving (`Surl.Api`'s `Directory.AddEntriesAsync`, the `add_ldap_entries` tool, the control endpoint's `POST /v1/directory`), and can reset and restore the directory (ADR-0158 decisions 3 to 5). The Glossary's "directory file" row, as BL-2075 corrected it, shows the wording.

Stand-in: documentation only; the seeds, reset, restore and events already exist, and nothing is added to them.

## Acceptance criteria

- [x] The Glossary's "LDAP directory" row says it is LDAP clients that cannot change the directory, and that the host can, in memory only, citing ADR-0158.
- [x] The Product Overview bullet and FR-049 say the same, and say the directory is empty only when it holds no entry from the file and none from a host seed.
- [x] The Roadmap's Milestone 5 "Delivers" text no longer calls the directory read-only or empty in memory without that limit.
- [x] All four places still say add, delete, modify and rename from an LDAP client are refused, wherever they said so before, and that surl never writes the file wherever they said so before.
- [x] The README's LDAP paragraph no longer calls the directory read-only or empty without those limits, and the diff changes no file but these five (and this task's own).

## Notes

- The Glossary, Product Overview and FR-049 also say that the hosts' reset and restore are refused when the
  instance's files are on disk: every host calls only the whole-instance operation (`SurlServer.ResetAsync`,
  `ResetServerTool`, `ComposedControlTarget.ResetAsync`), which stops at a content store on disk. Saying only
  "the host can reset and restore the directory", as Context does, would be untrue with `--directory`.
- They cite ADR-0158 decisions 3 to 6, not 3 to 5: decision 6 is the in-memory copy of the file.
- Follow-ups filed, in their own commit so this task's diff stays the five documents and this file:
  BL-2103 (Requirements.md names `Surl.Console` for code that is in `Surl.Composition`) and BL-2104
  (a dated note on ADR-0158 decision 6, after BL-2076 decides per-store reset).
- Verified 2026-11-28: `dotnet build` 0 warnings, 0 errors; fast tests no failures.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The Glossary, Product Overview, Requirements, Roadmap and README say LDAP clients cannot change the directory and the host can, in memory only (ADR-0158)
