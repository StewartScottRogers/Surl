---
id: BL-2085
title: Say in the Glossary, Product Overview, Requirements and Roadmap that the host can change the LDAP directory
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2075]
touches: [Documentation/Wiki/Glossary.md, Documentation/Product/Product-Overview.md, Documentation/Product/Requirements.md, Documentation/Planning/Roadmap.md, README.md]
requirement: FR-049
created: 2026-11-28
completed:
---
# BL-2085 — Say in the Glossary, Product Overview, Requirements and Roadmap that the host can change the LDAP directory

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

- [ ] The Glossary's "LDAP directory" row says it is LDAP clients that cannot change the directory, and that the host can, in memory only, citing ADR-0158.
- [ ] The Product Overview bullet and FR-049 say the same, and say the directory is empty only when it holds no entry from the file and none from a host seed.
- [ ] The Roadmap's Milestone 5 "Delivers" text no longer calls the directory read-only or empty in memory without that limit.
- [ ] All four places still say add, delete, modify and rename from an LDAP client are refused, wherever they said so before, and that surl never writes the file wherever they said so before.
- [ ] The README's LDAP paragraph no longer calls the directory read-only or empty without those limits, and the diff changes no file but these five (and this task's own).

## Notes

## Log

- 2026-11-28: Created.
