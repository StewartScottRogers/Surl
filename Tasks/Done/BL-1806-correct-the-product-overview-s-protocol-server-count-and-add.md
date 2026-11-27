---
id: BL-1806
title: Correct the product overview's protocol server count and add the Proxy folder to the README repository layout
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [README.md, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1806 — Correct the product overview's protocol server count and add the Proxy folder to the README repository layout

## Goal

The product overview counts and lists every protocol server project, and the README repository layout names every `Protocols/` category folder.

## Context

Found by BL-1799. `Documentation/Product/Product-Overview.md` says 15 protocol servers (section heading and the Layers table) and its scheme table lacks the HTTP proxy and SOCKS servers; there are 17 `Surl.Protocol.*.UnitLibrary` folders. The README "Repository layout" table has no `Protocols/Proxy/` row.

## Acceptance criteria

- [x] The protocol server count in the product overview equals the number of `Protocols/*/Surl.Protocol.<Name>.UnitLibrary` folders, and its scheme table covers the servers in `HttpProxy` and `Socks`.
- [x] The README repository layout table has a row for `Protocols/Proxy/`.

## Notes

- 2026-11-26: 17 folders match `Protocols/*/Surl.Protocol.*.UnitLibrary`. The overview says 17 in the section heading and the Layers table. The proxies answer no further scheme among the 29, and the section says so.
- Also corrected, in the same tables: the "Protocol servers" row's "Depends on" cell (rewritten from the 17 project files), the README `Protocols/Http/` row, and a new README `Protocols/DataStore/` row so every category folder has one.
- Gates: `dotnet build` 0 warnings, 0 errors; fast tests 0 failed. Markdown only.
- Filed the remaining inaccuracies as BL-1808, BL-1809 and BL-1810, with the board script directly rather than through `task-planner` (three small, fully known tasks).

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The product overview counts and lists all 17 protocol servers, and the README repository layout has a row for every Protocols category folder
