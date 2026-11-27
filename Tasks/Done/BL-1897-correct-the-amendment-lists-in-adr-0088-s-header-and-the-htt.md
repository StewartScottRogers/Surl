---
id: BL-1897
title: Correct the amendment lists in ADR-0088's header and the HTTP proxy's CLAUDE.md
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Documentation/Planning/Decisions/ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1897 — Correct the amendment lists in ADR-0088's header and the HTTP proxy's CLAUDE.md

## Goal

ADR-0088's header and the HTTP proxy library's `CLAUDE.md` name the ADR's amendments completely, and the header says
nothing false about ADR-0002 decision 3's table.

## Context

Found during BL-1874 on 2026-11-27. Prose only. No store, event or fault changes.

- ADR-0088's `Date` line lists Amendments 1 to 5 and 9 to 11. The file also has Amendments 6 (BL-1003, 2026-11-11),
  7 (BL-1004, 2026-11-11) and 8 (BL-1040, 2026-11-11).
- ADR-0088's `Amends` line says ADR-0002 decision 3's "table gains decision 1's row". BL-1836 corrects ADR-0002's
  matching note, because that table has no proxy row.
- `Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary/CLAUDE.md`, second paragraph, says the library was decided in
  ADR-0088 "and its Amendments 1 to 4"; later amendments (5 to 11) also decide the proxy's behaviour, and the same
  file cites Amendment 9.

## Acceptance criteria

- [x] ADR-0088's `Date` line lists Amendments 1 to 11, each with the task and date its own heading or text gives.
- [x] ADR-0088's `Amends` line agrees with ADR-0002 about whether decision 3's table has a proxy row.
- [x] The proxy's `CLAUDE.md` second paragraph names every ADR-0088 amendment that decides this library's behaviour.

## Notes

- Edited in the session instead of through `align-and-document`: three prose edits in two files. No `.cs` or project
  file changed and no decision was made, so no ADR.
- `Date` line: Amendments 10 and 11 name both the task in their heading (BL-556, BL-1664) and the task their text says
  recorded them (BL-1070, BL-1670).
- Proxy `CLAUDE.md`: 1 to 4, 9, 10 and 11 decide what the library does or answers. 5 to 8 decide `Surl.Cli` and
  `Surl.Composition`; the paragraph names them as that, so all eleven are accounted for.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: 83 test projects passed, none failed.
- Follow-up filed: BL-1904 (HTTP/2 doc comments cite Amendment 5 where Amendment 9 decides it).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. ADR-0088's header and the HTTP proxy's CLAUDE.md name all eleven amendments, and the Amends line agrees with ADR-0002's table
