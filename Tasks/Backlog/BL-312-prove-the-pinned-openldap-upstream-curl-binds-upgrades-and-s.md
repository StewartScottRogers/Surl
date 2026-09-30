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
completed:
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

- [ ] Integration tests exist for every case in Context and pass where the pinned OpenLDAP build is
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green on that platform
      (CI's run for it is green).
- [ ] Where the build is absent, the tests report Inconclusive with the pin named.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a socket.

## Notes

- Stewart approved lanes filing follow-up tasks beyond the plan (2026-09-30, "yes extra tasks").

## Log

- 2026-09-30: Created.
