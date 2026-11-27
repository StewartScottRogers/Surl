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
completed: 2026-11-26
---
# BL-1811 — Complete the library and user lists in the product overview and the README repository layout

## Goal

The product overview and the README repository layout name the libraries and the users of each library as the project files have them.

## Context

Found by BL-1806. In `Documentation/Product/Product-Overview.md`, Layers rows leave out the proxies as users of `Surl.LineProtocol` (both), `Surl.HttpMessage` (HTTP proxy) and the HTTP/2 libraries (HTTP proxy; SOCKS uses `Surl.Http2`); the "Message bus" row names fewer users than "Shared stores"; the "Rule 1" list of horizontal libraries omits `Surl.Cookies` and `Surl.Kerberos`; the "Project layout" table omits `Surl.Compression`, `Surl.HttpServing`, `Surl.WebSocket`, `Surl.Dtls`, `Surl.Cryptography.Srp`, `Surl.Tls13Stream` and `Surl.TlsSrp`. In the README "Repository layout" table, `Foundation/` omits the serving engine (`Surl.Core`) and `Surl.Compression`, `Cryptography/` omits DTLS and TLS-SRP, and `Protocols/Mail/` omits `Surl.LineProtocol`. Check each against the `ProjectReference` entries before editing.

## Acceptance criteria

- [x] Each Layers row and the "Message bus" row of the product overview name every user the project files show for that library.
- [x] The "Rule 1" list names `Surl.Cookies` and `Surl.Kerberos`, matching ADR-0002's table as later ADRs amend it.
- [x] The "Project layout" table names the seven libraries listed in Context.
- [x] The README repository layout rows for `Foundation/`, `Cryptography/` and `Protocols/Mail/` name the items listed in Context.

## Notes

- Users were checked against the production `ProjectReference` entries; test projects are not counted. Every row that lists who uses a library now lists all of them. Rows that only say what a layer depends on were checked as dependencies, and the ones that disagreed with the project files were corrected (Hosts, Composition, Command line, Services, DTLS, TLS 1.3).
- New Layers rows: Extended servers, Compression codecs, Hand-built TLS-SRP.
- Rule 1 now follows the allow-list in `ProtocolIsolationTests`, which omitted more than Context named. `Surl.Cookies` is on that list and the HTTP server references it (ADR-0189), but ADR-0002's table has no row for it; Rule 1 says so, and BL-1814 files the ADR correction. `Surl.Tls13Stream` is not on the list and only `Surl.Networking` references it, so Rule 1 leaves it out.
- Gates: `dotnet build` 0 warnings; fast tests 30134 passed in 83 assemblies.
- Follow-ups filed: BL-1814, BL-1815.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The product overview and README name the libraries and each library's users as the project files have them
