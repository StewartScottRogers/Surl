---
id: BL-287
title: Pin the OpenLDAP upstream curl 8.21.0 build and decide how CI obtains it
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-282, BL-284]
touches: [Documentation/Planning/Decisions, UpstreamCurlBuilds.json, .github/workflows/ci.yml, Record-CurlExchange.ps1, Surl.Conformance.UnitTests]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-287 — Pin the OpenLDAP upstream curl 8.21.0 build and decide how CI obtains it

## Goal

With Stewart's approval in BL-282, an accepted ADR pins an upstream curl 8.21.0 build whose LDAP
runs over OpenLDAP (`lib/openldap.c`), records how CI obtains and verifies it, and amends BL-284's
LDAP ADR with what that build sends, so the LDAP server can be proved against it.

## Context

- Why: BL-282's Context (only the `WinLDAP` Windows builds pinned today list `ldap ldaps`).
- If Stewart answers no in BL-282, this task records that in an ADR instead (LDAP proved on
  Windows only, `lib/openldap.c`'s requests answered from RFC 4511, RFC 4513 and RFC 4422 without
  measurement), moves BL-312 to `Deferred` with the ADR as the reason, and pins nothing.
- Pattern: ADR-0016 (Linux and macOS builds: which build, where it lives, how CI downloads, caches
  and verifies it by SHA-256 before it runs) and ADR-0030 (a supplementary build for one protocol,
  its role in `UpstreamCurlBuilds.json`). Decide the build's origin within Stewart's approval
  (a third-party static build that includes OpenLDAP, or tag `curl-8_21_0` built from source with
  OpenLDAP in CI), its `role` (`supplementary`, for `ldap` and `ldaps` only), its platforms, and
  its path.
- Measure with `Record-CurlExchange.ps1 -Curl <path>` (it runs any pinned SHA-256) the cases BL-284
  measured on Windows, plus what only `lib/openldap.c` does: `--ssl` and `--ssl-reqd` (the
  `StartTLS` extended operation), the root-DSE search for `supportedSASLMechanisms`,
  `--login-options AUTH=<mech>` for each mechanism BL-284's ADR offers, `--sasl-ir`,
  `--oauth2-bearer`; and amend BL-284's ADR where the bytes differ from what it decided.
- `UpstreamCurlBuildPinsTests` and `UpstreamCurlLocatorTests` in `Surl.Conformance.UnitTests` read
  `UpstreamCurlBuilds.json`; keep them green (a supplementary Linux entry must not change which
  build `Locate` returns by default, ADR-0030 decision 5).

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude under
      Stewart's delegation", citing Stewart's answer in BL-282, decides the build, its role, path
      and how CI obtains it, and records each measurement (build, SHA-256, arguments, date,
      transcript excerpt).
- [ ] `UpstreamCurlBuilds.json` pins the build by SHA-256 with its `version`, `protocols` and
      `features` lines as measured, and `.github/workflows/ci.yml` obtains and verifies it.
- [ ] BL-284's ADR carries an "Amended" line naming the new ADR, if the measurement changed a
      decision.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the ADR; the fast tests are green.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
