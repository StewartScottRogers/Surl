---
id: BL-338
title: Measure LDAP SASL GSSAPI against an OpenLDAP upstream curl build that has GSS-API, and settle its security layer
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-287]
touches: [Documentation/Planning/Decisions, UpstreamCurlBuilds.json, Build-OpenLdapUpstreamCurl.ps1, .github/workflows/ci.yml]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-338 — Measure LDAP SASL GSSAPI against an OpenLDAP upstream curl build that has GSS-API, and settle its security layer

## Goal

An accepted ADR records what upstream curl 8.21.0's `lib/openldap.c` sends for `--login-options
AUTH=GSSAPI` against a loopback KDC (ADR-0065) - in particular which RFC 4752 security layers its
last token accepts - and confirms or amends ADR-0072 decision 4's `GSSAPI` line ("no security layer"
only).

## Context

- ADR-0072 decision 4 left the `GSSAPI` security-layer question to BL-287's measurement. The build
  BL-287 pinned (ADR-0076) has no GSS-API: its `Features` line has no `Kerberos` or `GSS-API`, and
  `AUTH=GSSAPI` ends 67 `Login denied` without a bind (ADR-0076, its measurements). So it stays
  unmeasured.
- Measuring it needs a variant of `Build-OpenLdapUpstreamCurl.ps1` that also links MIT Kerberos
  (`--with-gssapi`) from a pinned source tarball, and a new pin. Stewart's approval in BL-282 covers
  an OpenLDAP build of tag `curl-8_21_0` built from source in CI; decide in the ADR whether the GSS
  variant replaces ADR-0076's pin or sits beside it (one build per protocol and role is simpler,
  ADR-0030 decision 5), and whether it is still within that approval.
- Measure through `Record-CurlExchange.ps1 -Ldap -KerberosTestKdc` (extend the KDC switch to run on
  Linux if needed; today it refuses outside Windows).

## Acceptance criteria

- [ ] An ADR in `Documentation/Planning/Decisions/`, "Decided by Claude under Stewart's delegation",
      records the GSSAPI measurements (build, SHA-256, arguments, date, transcript excerpt) and
      confirms or amends ADR-0072 decision 4's `GSSAPI` line, with an "Amended" line on ADR-0072 if
      it changes.
- [ ] `Documentation/Planning/Decisions/README.md` indexes it; the fast tests are green.

## Notes

- Filed by BL-287 (2026-09-30) as follow-up work; lanes may file extra tasks (Stewart, 2026-09-30).

## Log

- 2026-09-30: Created.
