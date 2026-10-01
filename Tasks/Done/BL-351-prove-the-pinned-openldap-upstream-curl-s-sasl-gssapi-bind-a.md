---
id: BL-351
title: Prove the pinned OpenLDAP upstream curl's SASL GSSAPI bind against surl on CI's Linux leg
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-342]
touches: [Surl.Conformance.UnitTests, .github/workflows/ci.yml, Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Documentation/Planning/Decisions/ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md, Documentation/Planning/Decisions/ADR-0078-ldap-sasl-gssapi-measured-against-an-openldap-build-with-mit-kerberos.md]
requirement: FR-052
created: 2026-10-01
completed: 2026-10-01
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

- [x] A test in `Surl.Conformance.UnitTests` runs the pinned OpenLDAP build with
      `--login-options AUTH=GSSAPI -u tester@SURL.TEST:` against surl serving `ldap` with a keytab
      from the test KDC, and asserts exit 0 and the searched entry on stdout; elsewhere it reports
      Inconclusive, as BL-312's tests do.
- [x] `.github/workflows/ci.yml`'s Linux leg provides what the test needs (`kinit`), and the fast
      tests stay green on all three platforms.

## Notes

- **The test:** `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_GssapiWithATicketFromTheTestKdc_Exits0WithTheBaseEntry`.
  Inconclusive off linux-x64 (no OpenLDAP pin, as the class's other tests) and where no `kinit`
  is on `PATH`. The old `SaslBind_Gssapi_Exits67BecauseTheBuildHasNoGssApi` was false since
  ADR-0078's re-pin; renamed `SaslBind_GssapiWithAUserNamingNoRealm_Exits67WithoutABind` (ADR-0078's
  table: no domain in the user, no bind, 67).
- **Choice: an ephemeral KDC port, not 88.** CI's runner is not root, so port 88 cannot be bound;
  `KerberosTestKdcOnLoopback.StartWithCredentialCacheAsync` binds TCP on port 0 and UDP on the same
  port, writes the measured `krb5.conf` naming it, runs `kinit` with the password on stdin, and
  exposes `ClientEnvironment` (`KRB5_CONFIG`, `KRB5CCNAME`). `PinnedUpstreamCurl` gains
  `RunBuildLinkedAgainstWithEnvironmentAsync`. The Windows `StartAsync` path is unchanged.
- **Defect found and fixed (touches widened):** `GssapiSaslExchange.ServiceOf` mapped `ldap`/`ldaps`
  to `pop`, so surl refused every LDAP GSSAPI ticket (`Kerberos: no key for ldap/ldap.surl.test`).
  Added `Surl.Authentication.UnitLibrary`/`.UnitTests` (failing rows `ldap`, `ldaps` first), and
  ADR-0057 (Amendment 2, the service table gains `ldap`) and ADR-0078 (decision 4 now true) to
  `touches`; the only other Doing task, BL-344, touches only ADR-0072.
- **Verified on Linux** before CI: built the pinned OpenLDAP curl with
  `Build-OpenLdapUpstreamCurl.ps1` (SHA-256 `62061C58...` matches the pin), ran the class in
  `mcr.microsoft.com/dotnet/sdk:10.0` as a non-root user with `krb5-user`: 47/47 pass.
- CI: `Install kinit` step (`apt-get install krb5-user`, noninteractive) on the
  `openldap-upstream-curl` leg before the conformance tests.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. The pinned OpenLDAP upstream curl binds to surl with SASL GSSAPI (test KDC ticket via kinit) and searches, exit 0, on CI's Linux leg; surl's GSSAPI now answers the ldap service
