---
id: BL-1966
title: Correct the Kerberos and OpenLDAP proof claims in the LDAP library notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1949]
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1966 — Correct the Kerberos and OpenLDAP proof claims in the LDAP library notes

## Goal

The LDAP library's `CLAUDE.md` says that a Kerberos `GSS-SPNEGO` bind is accepted with `--keytab` and that the
OpenLDAP-backed upstream curl build proves the server.

## Context

Found while working BL-1949 (2026-11-27), read from source. Project-notes text only. Line numbers are as of that date
and approximate. BL-1949 edits other lines of the same file, hence the dependency.

`Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md`:

- About lines 74-75: "Kerberos inside `GSS-SPNEGO` is answered with NTLM selected (ADR-0040's rule); BL-327 is to
  decide and build Kerberos there." BL-327 is done. ADR-0072 Amendment 1: with `--keytab`,
  `Surl.Authentication`'s `GssSpnegoSaslExchange` accepts a token selecting Kerberos in one leg for service `ldap`,
  with an RFC 4121 security layer (`KerberosSaslSecurityLayer`) when the client asks for one; without `--keytab` it is
  answered by ADR-0040's rule with NTLM selected.
  `Conformance/Surl.Conformance.UnitTests/UpstreamCurlBindsToSurlOverLdapWithKerberosTests.cs` proves it against the
  Windows reference build (curl 8.21.0 over `WinLDAP`).
- About lines 77-80: "Nothing yet proves it against the OpenLDAP-backed build ADR-0076 pins ... that is BL-312's."
  BL-312 is done: `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.cs`, beside the file above, runs `STARTTLS`,
  `ldaps`, the root-DSE SASL discovery, SASL binds and the anonymous bind against that build (`linux-x64` only;
  Inconclusive elsewhere). `Documentation/Product/Product-Overview.md`, "Built for Phase 5: LDAP, SMB and RTSP" and
  the conformance results after it, records the same.

No store, event or fault changes: documentation only.

## Acceptance criteria

- [ ] The `CLAUDE.md` says a Kerberos `GSS-SPNEGO` bind is accepted with `--keytab` (ADR-0072 Amendment 1) and answered with NTLM selected without it.
- [ ] The `CLAUDE.md` names `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests` as the proof against the OpenLDAP-backed build, and says that build is pinned for `linux-x64` only.
- [ ] No line of the `CLAUDE.md` describes BL-327 or BL-312 as work still to do.
- [ ] Only that `CLAUDE.md` changes.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
