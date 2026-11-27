---
id: BL-1931
title: Correct the certificate-only StartTLS claims in the LDAP server's comments and notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1924]
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1931 — Correct the certificate-only StartTLS claims in the LDAP server's comments and notes

## Goal

The LDAP library's comments and `CLAUDE.md` say `StartTLS` is available with a certificate or under TLS-SRP.

## Context

Found while working BL-1924 (2026-11-27), read from source. Comment and project-notes text only; no behaviour change and
no string literal changes. Line numbers are as of that date and approximate. `Surl.Composition`
(`SurlServerComposition.LoadLdapServerAsync`) gives the server `isTlsUpgradeAvailable` from
`ServerTlsComposition.IsTlsUpgradeAvailable`: a certificate (`--cert`, `--self-signed`) or TLS-SRP (ADR-0122
decision 5). The library does not reference `Surl.Composition`, so name the type in `<c>`, not `<see cref>`.

In `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary`:

- `LdapProtocolServer.cs`, the "TLS." remarks, about lines 46-50 ("constructed with a certificate available",
  "Without a certificate it is an unknown extended operation").
- `LdapProtocolServer.cs`, the three `isTlsUpgradeAvailable` params, about lines 102, 116 and 142.
- `LdapSession.cs`, about line 217: "offered only by a server with a certificate".
- `CLAUDE.md`, about line 39: the root DSE lists `StartTLS` "with a certificate".

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] The "TLS." remarks and the three `isTlsUpgradeAvailable` params of `LdapProtocolServer` name both a certificate and TLS-SRP, and say `StartTLS` is an unknown extended operation only when no upgrade is available.
- [ ] The `LdapSession` comment and the `CLAUDE.md` root DSE sentence name both a certificate and TLS-SRP.
- [ ] No comment or `CLAUDE.md` line in the library says `StartTLS` depends on a certificate alone.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
