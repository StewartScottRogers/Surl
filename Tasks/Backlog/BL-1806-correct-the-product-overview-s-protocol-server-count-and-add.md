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
completed:
---
# BL-1806 — Correct the product overview's protocol server count and add the Proxy folder to the README repository layout

## Goal

The product overview counts and lists every protocol server project, and the README repository layout names every `Protocols/` category folder.

## Context

Found by BL-1799. `Documentation/Product/Product-Overview.md` says 15 protocol servers (section heading and the Layers table) and its scheme table lacks the HTTP proxy and SOCKS servers; there are 17 `Surl.Protocol.*.UnitLibrary` folders. The README "Repository layout" table has no `Protocols/Proxy/` row.

## Acceptance criteria

- [ ] The protocol server count in the product overview equals the number of `Protocols/*/Surl.Protocol.<Name>.UnitLibrary` folders, and its scheme table covers the servers in `HttpProxy` and `Socks`.
- [ ] The README repository layout table has a row for `Protocols/Proxy/`.

## Notes

## Log

- 2026-11-26: Created.
