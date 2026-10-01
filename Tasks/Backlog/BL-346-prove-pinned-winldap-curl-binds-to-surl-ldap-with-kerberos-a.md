---
id: BL-346
title: Prove pinned WinLDAP curl binds to surl ldap with Kerberos and --keytab
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-311, BL-327]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-346 — Prove pinned WinLDAP curl binds to surl ldap with Kerberos and --keytab

## Goal

A `[TestCategory("Integration")]` test proves the pinned Windows reference build's `--negotiate`
bind reaches a live `surl ldap://... --keytab` through Kerberos and searches it over the sealed
RFC 4121 layer, exiting 0 with the entry, as ADR-0072 Amendment 1 decides.

## Context

- ADR-0072 Amendment 1 (BL-327): `WinLDAP` uses Kerberos inside `GSS-SPNEGO` for
  `ldap://localhost:<port>`, asking for `ldap/<the machine's host name>:<port>` in the user's realm;
  surl answers with `--keytab` in one leg and seals with `KerberosSecurityContext.Seal`. BL-327
  measured it against the recorder, not against surl, since `surl` did not serve `ldap` yet (BL-310).
- Harness: BL-311's LDAP conformance tests, plus the Kerberos ones of BL-269 (`SURL.TEST` realm
  mapping of BL-265, `Surl.Kerberos.TestKdc` for the KDC and its keytab); Inconclusive without the
  realm mapping, port 88, or the pinned build, by ADR-0065 decision 3.
- Service principal: `ldap/<Dns.GetHostName()>:<port>`, credentials `-u tester@SURL.TEST:<password>`,
  an account named `tester@SURL.TEST`.
- If `surl` refuses `--keytab` with an `ldap://` listen URL, or the `ldap` `--aihelp` topic does
  not say Kerberos is answered, file that as its own task for the projects that own it.

## Acceptance criteria

- [ ] An Integration test in `Surl.Conformance.UnitTests` runs the pinned build with `--negotiate -u
      tester@SURL.TEST:<password> ldap://localhost:<port>/<base>?cn?base` against `surl --keytab`
      and asserts exit 0 and the entry on stdout; it is Inconclusive where ADR-0065 decision 3 says.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a socket.

## Notes

- Filed by BL-327 (ADR-0072 Amendment 1).

## Log

- 2026-09-30: Created.
