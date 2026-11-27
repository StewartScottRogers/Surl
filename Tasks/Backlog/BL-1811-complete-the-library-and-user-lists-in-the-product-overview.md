---
id: BL-1811
title: Complete the library and user lists in the product overview and the README repository layout
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [README.md, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-1811 — Complete the library and user lists in the product overview and the README repository layout

## Goal

The product overview and the README repository layout name the libraries and the users of each library as the project files have them.

## Context

Found by BL-1806. In `Documentation/Product/Product-Overview.md`, Layers rows leave out the proxies as users of `Surl.LineProtocol` (both), `Surl.HttpMessage` (HTTP proxy) and the HTTP/2 libraries (HTTP proxy; SOCKS uses `Surl.Http2`); the "Message bus" row names fewer users than "Shared stores"; the "Rule 1" list of horizontal libraries omits `Surl.Cookies` and `Surl.Kerberos`; the "Project layout" table omits `Surl.Compression`, `Surl.HttpServing`, `Surl.WebSocket`, `Surl.Dtls`, `Surl.Cryptography.Srp`, `Surl.Tls13Stream` and `Surl.TlsSrp`. In the README "Repository layout" table, `Foundation/` omits the serving engine (`Surl.Core`) and `Surl.Compression`, `Cryptography/` omits DTLS and TLS-SRP, and `Protocols/Mail/` omits `Surl.LineProtocol`. Check each against the `ProjectReference` entries before editing.

## Acceptance criteria

- [ ] Each Layers row and the "Message bus" row of the product overview name every user the project files show for that library.
- [ ] The "Rule 1" list names `Surl.Cookies` and `Surl.Kerberos`, matching ADR-0002's table as later ADRs amend it.
- [ ] The "Project layout" table names the seven libraries listed in Context.
- [ ] The README repository layout rows for `Foundation/`, `Cryptography/` and `Protocols/Mail/` name the items listed in Context.

## Notes

## Log

- 2026-11-26: Created.
