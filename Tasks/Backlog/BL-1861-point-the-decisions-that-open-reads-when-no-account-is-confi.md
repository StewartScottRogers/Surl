---
id: BL-1861
title: Point the decisions that open reads when no account is configured at ADR-0132 amendment 5
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1855]
touches: [Documentation/Planning/Decisions/ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md, Documentation/Planning/Decisions/ADR-0071-how-the-websocket-server-answers-upstream-curl.md, Documentation/Planning/Decisions/ADR-0074-how-the-rtsp-server-answers-upstream-curl.md, Documentation/Planning/Decisions/ADR-0092-how-the-dns-server-answers-dns-and-its-encrypted-transports.md, Documentation/Planning/Decisions/ADR-0097-how-the-coap-server-answers-coap-and-coaps.md, Documentation/Planning/Decisions/ADR-0149-how-an-ldap-directory-serves-as-an-optional-account-source.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1861 — Point the decisions that open reads when no account is configured at ADR-0132 amendment 5

## Goal

No decision reads as if removing the last account while a server runs opens its reads.

## Context

- ADR-0132 amendment 5.
- Passages: ADR-0032 section 4 ("With no account configured"), ADR-0071 "Anonymous upgrade is a read", ADR-0074
  "Reads need no login while no account is configured", ADR-0092 decision 5 "Logins", ADR-0097 decision 6 and its
  amendment 3 (the "Removal" and "Alternative rejected" bullets are superseded), and ADR-0149 amendment 1's `Surl.Api`
  bullet, which gives the old directory warning and a method name BL-1855 renames.

## Acceptance criteria

- [ ] Each passage named in Context carries a short pointer to ADR-0132 amendment 5 saying what it changes; the
      original wording stays as the record.
- [ ] ADR-0097 amendment 3's "Removal" and "Alternative rejected" bullets are marked superseded.
- [ ] ADR-0149 says the warning it quotes is replaced by ADR-0132 amendment 5's line, and names
      `IsLoginRequiredToRead` as what the HTTP family asks now.
- [ ] The index rows of the changed decisions in the decisions `README.md` point to amendment 5.

## Notes

## Log

- 2026-11-27: Created.
