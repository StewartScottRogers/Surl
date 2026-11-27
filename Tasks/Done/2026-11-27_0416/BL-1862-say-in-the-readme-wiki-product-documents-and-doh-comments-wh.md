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
completed: 2026-11-27
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

- [x] Each statement named in Context says that a scheme which has had an account keeps asking for a login when the
      last one is removed, or links to ADR-0132 amendment 5 for it.
- [x] The Glossary's "anonymous read" row says so and names `IsLoginRequiredToRead` as what decides it.
- [x] No `.cs` change beyond comments, and `dotnet build` is clean.

## Notes

- Delivered by `align-and-document`. Changed: root `README.md`, the Glossary's "anonymous read" row, the
  Product-Overview WebSocket and RTSP login bullets, FR-014 and FR-052's WebSocket and RTSP clauses, the DoH comment
  (moved from `Unauthenticated` to the `JudgeAsync` call it describes) and that library's `CLAUDE.md`.
- The "with no account configured every login is refused" sentences elsewhere stay: they are about logins, not open
  reads.
- Verify: build 0 warnings, format check clean, fast tests 30267 passed, 0 failed, 3 skipped.
- False statements outside `touches` and not named by BL-1859, 1860, 1864, 1865 or 1866 are filed as BL-1898,
  BL-1899 and BL-1900. Filed with the board script in this session, without `task-planner`, since each is one
  statement.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The README, Glossary, product documents and DoH comments say reads need no login only on a scheme that has never had an account
