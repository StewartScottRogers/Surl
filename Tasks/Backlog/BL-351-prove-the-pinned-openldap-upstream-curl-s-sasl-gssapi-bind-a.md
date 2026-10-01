---
id: BL-351
title: Prove the pinned OpenLDAP upstream curl's SASL GSSAPI bind against surl on CI's Linux leg
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-342]
touches: [Surl.Conformance.UnitTests, .github/workflows/ci.yml]
requirement: FR-052
created: 2026-10-01
completed:
---
# BL-351 — Prove the pinned OpenLDAP upstream curl's SASL GSSAPI bind against surl on CI's Linux leg

## Goal

A conformance test on CI's Linux leg has the pinned OpenLDAP upstream curl (ADR-0078's GSS-API
build) bind to `surl --keytab ... --auth gssapi` with SASL `GSSAPI` and search, exit 0.

## Context

- ADR-0078 measured what curl 8.21.0's `lib/openldap.c` sends for `--login-options AUTH=GSSAPI`
  against the recorder: an AP-REQ without mutual authentication, then the unwrapped answer
  `01000000` (no security layer, max size 0) to the server's offer; surl's LDAP server offers
  exactly that (ADR-0072 decision 4). It is not yet proved against a live surl.
- curl's MIT GSS-API reads a credential cache, not `-u`'s password: the test needs the
  hand-built test KDC (`Surl.Kerberos.TestKdc`, ADR-0065), a `krb5.conf` and a `kinit` (Ubuntu's
  `krb5-user` on the runner: an OS package for the CI image, not a NuGet package), and port 88
  (or a KDC port the `krb5.conf` names). `Record-CurlExchange.ps1`'s `Initialize-KerberosClient`
  shows the `krb5.conf` that works. The user must name its realm (`-u tester@SURL.TEST:`), or
  curl does not try `GSSAPI` (67).
- The host must be a name the service principal names (`--resolve ldap.surl.test:<port>:127.0.0.1`
  and `ldap/ldap.surl.test`); `127.0.0.1` ends 94.

## Acceptance criteria

- [ ] A test in `Surl.Conformance.UnitTests` runs the pinned OpenLDAP build with
      `--login-options AUTH=GSSAPI -u tester@SURL.TEST:` against surl serving `ldap` with a keytab
      from the test KDC, and asserts exit 0 and the searched entry on stdout; elsewhere it reports
      Inconclusive, as BL-312's tests do.
- [ ] `.github/workflows/ci.yml`'s Linux leg provides what the test needs (`kinit`), and the fast
      tests stay green on all three platforms.

## Notes

## Log

- 2026-10-01: Created.
