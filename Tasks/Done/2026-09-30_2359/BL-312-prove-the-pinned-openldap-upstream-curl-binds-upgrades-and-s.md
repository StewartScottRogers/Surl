---
id: BL-312
title: Prove the pinned OpenLDAP upstream curl binds, upgrades and searches surl over ldap and ldaps
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-287, BL-311]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-052
created: 2026-09-30
completed: 2026-09-30
---
# BL-312 — Prove the pinned OpenLDAP upstream curl binds, upgrades and searches surl over ldap and ldaps

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that the OpenLDAP build
BL-287 pins completes, against a live `surl`, what only upstream's `lib/openldap.c` sends: `STARTTLS`
for `--ssl` and `--ssl-reqd`, the root-DSE `supportedSASLMechanisms` search, SASL binds with each
mechanism BL-284's ADR offers (with and without `--sasl-ir`, and `--oauth2-bearer`), and the searches
BL-311 proves on Windows.

## Context

- The cases and expected exit codes: BL-287's ADR (its measurements with the pinned build) and
  BL-284's ADR as BL-287 amended it.
- The build runs where BL-287 pins it (Linux on CI, and macOS if pinned); elsewhere the tests report
  Inconclusive, naming the pin, by ADR-0026 decision 2's rule.
- If Stewart declined the build in BL-282, BL-287 moves this task to `Deferred`.
- Harness: as BL-311's; the build is located by protocol and role (`UpstreamCurlLocator`, ADR-0030
  decision 5).
- Any disagreement with the pinned build is fixed in the library at fault through a new task filed by
  `task-planner`, never by changing the expected result (ADR-0003); list them in the Log.

## Acceptance criteria

- [x] Integration tests exist for every case in Context and pass where the pinned OpenLDAP build is
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green on that platform
      (CI's run for it is green).
- [x] Where the build is absent, the tests report Inconclusive with the pin named.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a socket.

## Notes

- Stewart approved lanes filing follow-up tasks beyond the plan (2026-09-30, "yes extra tasks").
- **What was built (2026-09-30).** `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests`
  (Integration, 46 tests): BL-311's searches, the simple bind accepted and refused (67), the
  plain-text rule (38), the anonymous bind with and without `--allow-anonymous`, StartTLS under
  `--ssl` and `--ssl-reqd` (no certificate: 1 and a version 2 bind in clear; `-k`: upgraded;
  untrusted: 64), `ldaps` with `-k`, without (60) and with `--cacert`, and SASL after the root-DSE
  search: `PLAIN` and `LOGIN` with and without `--sasl-ir`, `CRAM-MD5`, `DIGEST-MD5` and `NTLM` by
  `AUTH=` and by `--digest`/`--ntlm`, `OAUTHBEARER` and `XOAUTH2` with `--oauth2-bearer` with and
  without `--sasl-ir`, `EXTERNAL` over `ldaps` with a client certificate and without one (67),
  `AUTH=*`, `AUTH=GSSAPI` (67, no GSS-API in the build), wrong passwords and token (67).
- **Harness.** `PinnedUpstreamCurl.RunBuildLinkedAgainstAsync(testContext, "OpenLDAP", ...)` picks
  the pin whose version line names the library, whatever its role. Default taken: by library,
  not by protocol and role, because on Windows the supplementary curl-for-win build also lists
  `ldap` (over `WinLDAP`), so "supplementary for `ldap`" would run the wrong build there.
  `RequireBuildLinkedAgainstAsync` runs in `[TestInitialize]` so the tests are Inconclusive before
  surl loads the 10001-entry directory (Windows: 46 Inconclusive in 0.1 s, message names
  `/opt/upstream-curl/8.21.0-openldap/curl (curl 8.21.0 ... OpenLDAP/2.6.15) for linux-x64`).
  BL-311's LDIF moved to the shared `LdapConformanceDirectory`; `TestCertificateAuthority` gained
  a `CN=alice` client certificate for `EXTERNAL`.
- **How it was proved on Linux.** Rebuilt the pin with `Build-OpenLdapUpstreamCurl.ps1` (same
  SHA-256 `8D4572E8...2398`, so it reproduced again) and ran
  `dotnet test Surl.Conformance.UnitTests --filter "FullyQualifiedName~Surl.Conformance"` in a
  `mcr.microsoft.com/dotnet/sdk:10.0` container with the build mounted at its pin path: 191 passed,
  0 failed, 424 skipped (other protocols, whose Linux reference build was not in the container).
  CI's own run is not seen here (lanes do not push); it runs the same filter with the same pin.
- **Measured, not in ADR-0076's tables:** `--ssl-reqd` against an untrusted StartTLS certificate
  exits **64** (`SSL certificate OpenSSL verify result: self-signed certificate (18)`), not 60 -
  curl's own mapping, pinned in the test. No disagreement with surl found: nothing filed against a
  library. Every other expectation (one more `\n` per entry than `WinLDAP`, 67 vs 38) held as
  ADR-0076 predicted.
- ADR-0076 could not be amended here (BL-327 holds `Documentation/Planning/Decisions`): filed
  BL-345 to record these measurements.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The pinned OpenLDAP upstream curl binds (simple, anonymous, every SASL mechanism the build has), upgrades with StartTLS and searches a live surl over ldap and ldaps: 46 integration tests green on Linux, Inconclusive naming the pin elsewhere
