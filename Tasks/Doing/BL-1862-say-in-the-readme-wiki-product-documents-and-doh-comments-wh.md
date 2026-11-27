---
id: BL-1862
title: Say in the README, wiki, product documents and DoH comments when reads still need no login
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1863]
touches: [README.md, Documentation/Wiki/Glossary.md, Documentation/Product/Product-Overview.md, Documentation/Product/Requirements.md, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1862 — Say in the README, wiki, product documents and DoH comments when reads still need no login

## Goal

The product documents, the glossary, the root README and the DNS over HTTPS comments say reads need no login only on a
scheme that has never had an account, a removal while the server runs included.

## Context

- ADR-0132 amendment 5, decisions 1 and 2.
- Statements: the root `README.md` "Surl is secure by default" paragraph; the Glossary's "anonymous read" row; the
  Product-Overview bullets saying "no account configured" for WebSocket and RTSP reads; FR-014;
  `DnsOverHttpsHandler`'s comment "with no account configured the session proceeds" and that library's `CLAUDE.md`
  "no account, no challenge".

## Acceptance criteria

- [ ] Each statement named in Context says that a scheme which has had an account keeps asking for a login when the
      last one is removed, or links to ADR-0132 amendment 5 for it.
- [ ] The Glossary's "anonymous read" row says so and names `IsLoginRequiredToRead` as what decides it.
- [ ] No `.cs` change beyond comments, and `dotnet build` is clean.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
