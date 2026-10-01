---
id: BL-342
title: Measure LDAP SASL GSSAPI against an OpenLDAP upstream curl build that has GSS-API, and settle its security layer
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-287]
touches: [Documentation/Planning/Decisions, UpstreamCurlBuilds.json, Build-OpenLdapUpstreamCurl.ps1, .github/workflows/ci.yml, Record-CurlExchange.ps1, Run-KerberosAcceptor.cs]
requirement: FR-049
created: 2026-09-30
completed: 2026-10-01
---
# BL-342 — Measure LDAP SASL GSSAPI against an OpenLDAP upstream curl build that has GSS-API, and settle its security layer

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

- [x] An ADR in `Documentation/Planning/Decisions/`, "Decided by Claude under Stewart's delegation",
      records the GSSAPI measurements (build, SHA-256, arguments, date, transcript excerpt) and
      confirms or amends ADR-0072 decision 4's `GSSAPI` line, with an "Amended" line on ADR-0072 if
      it changes.
- [x] `Documentation/Planning/Decisions/README.md` indexes it; the fast tests are green.

## Notes

- Filed by BL-287 (2026-09-30) as follow-up work; lanes may file extra tasks (Stewart, 2026-09-30).
- 2026-10-01: Outcome in ADR-0078. `Build-OpenLdapUpstreamCurl.ps1` now also builds MIT Kerberos
  1.22.2 (GPG-verified) and links curl `--with-gssapi`; SHA-256 `62061C58...7E55`, the same three
  times; it replaces ADR-0076's pin (one ldap pin per platform and role; every re-run ADR-0076 case
  unchanged). Judged within Stewart's BL-282 approval: same tag, built from source, no download of
  a curl binary.
- `touches` gained `Record-CurlExchange.ps1` (`-KerberosTestKdc` on Linux: `krb5.conf` and `kinit`;
  `-Ldap` answers RFC 4752 `GSSAPI` binds; new `-LdapGssapiSecurityLayers`) and
  `Run-KerberosAcceptor.cs` (`gssapi` and `sign` commands): the measurement could not run without
  them, and no task in Doing named either.
- Measured in a `mcr.microsoft.com/dotnet/sdk:10.0` container (root for port 88; WSL has no .NET
  SDK), `kinit` from Ubuntu's `krb5-user` installed in that throwaway container only. curl asks no
  mutual authentication and answers every offer `01000000` (no layer, max size 0); without the
  "no layer" bit it aborts with an empty-mechanism bind and searches unbound. ADR-0072 decision 4's
  `GSSAPI` line confirmed unchanged.
- Filed BL-351: prove the GSSAPI bind against a live surl on CI's Linux leg.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. LDAP SASL GSSAPI measured against a re-pinned OpenLDAP+MIT Kerberos build (ADR-0078); ADR-0072's no-security-layer line confirmed
