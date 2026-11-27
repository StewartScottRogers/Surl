---
id: BL-1987
title: Correct the constructor and fixed-directory statements in the LDAP library notes and ADR-0072
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1966]
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1987 — Correct the constructor and fixed-directory statements in the LDAP library notes and ADR-0072

## Goal

The LDAP library's `CLAUDE.md` and ADR-0072 state the server's constructor parameters correctly and no longer say,
without qualification, that the directory never changes once loaded.

## Context

Found while working BL-1966 (2026-11-27), read from source. Documentation only. Line numbers are as of that date.

- `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md`, the paragraph starting "On top of both is the
  server": it says the public constructor and `LoadAsync` each take `IAuthenticationPolicy`,
  `ISaslAuthenticationPolicy` and `isTlsUpgradeAvailable`. `LdapProtocolServer.cs` (about lines 108 and 151-158)
  shows both also require a `TimeProvider` and take an optional `ISurlEventPublisher`.
- The same file's directory paragraphs do not mention that a host can change the in-process directory:
  `LdapProtocolServer.SeedDirectoryEntries`, `ResetDirectory`, `TakeDirectorySnapshot` and `RestoreDirectory`
  (about lines 204-230), over `LdapDirectory.SeedEntries`, `Reset`, `TakeSnapshot` and `Restore`. ADR-0158 decides
  this. "Read-only directory" stays true of what an LDAP client can do.
- `ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md`, decision 1 (about line 270):
  "it never changes once loaded". Add a dated amendment note naming ADR-0158; do not rewrite the original decision.

Stand-in: no store, event or fault changes. The notes only describe the seeding, reset and snapshot calls that exist.

## Acceptance criteria

- [ ] The `CLAUDE.md` lists `TimeProvider` and the optional `ISurlEventPublisher` among what the public constructor and `LoadAsync` take.
- [ ] The `CLAUDE.md` names the four public seed, reset and snapshot methods of `LdapProtocolServer`, cites ADR-0158, and says LDAP operations cannot change the directory.
- [ ] ADR-0072 carries a dated amendment note saying ADR-0158 lets a host change the loaded directory, beside or referenced from the "it never changes once loaded" sentence.
- [ ] Only those two files change.

## Notes

## Log

- 2026-11-27: Created.
