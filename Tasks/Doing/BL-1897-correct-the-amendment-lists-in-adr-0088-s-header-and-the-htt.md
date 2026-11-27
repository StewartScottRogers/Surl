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
completed:
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

- [ ] ADR-0088's `Date` line lists Amendments 1 to 11, each with the task and date its own heading or text gives.
- [ ] ADR-0088's `Amends` line agrees with ADR-0002 about whether decision 3's table has a proxy row.
- [ ] The proxy's `CLAUDE.md` second paragraph names every ADR-0088 amendment that decides this library's behaviour.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
