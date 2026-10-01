---
id: BL-311
title: Prove the pinned WinLDAP upstream curl searches surl over ldap and ldaps
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-310]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-049
created: 2026-09-30
completed: 2026-09-30
---
# BL-311 — Prove the pinned WinLDAP upstream curl searches surl over ldap and ldaps

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that the pinned Windows
reference build (upstream curl 8.21.0 over `WinLDAP`) searches a live `surl` over `ldap` and `ldaps`
in every case BL-284's ADR lists, with the exit codes and output it expects.

## Context

- The cases and expected exit codes: BL-284's ADR's list (at least a base, a one-level and a subtree
  search with a filter and attributes, no entries, `-u` simple bind accepted and refused (38),
  plain-text bind refused without `--allow-plaintext-auth` and accepted with it, `--ntlm`,
  `--negotiate` and `--digest` binds as the ADR decides them, no `-u`, `ldaps://` with `-k`,
  `--ssl-reqd` (curl's own 4), the directory loaded from a temporary `--directory`).
- Only the Windows builds list `ldap` (`UpstreamCurlBuilds.json`): on Linux and macOS these tests
  report Inconclusive, naming the pin, by ADR-0026 decision 2's rule (the pin's protocol list, written
  once beside `PinnedUpstreamCurl.RunAsync`; reuse it if an earlier conformance task added it).
  BL-312 covers `lib/openldap.c` once BL-287 pins a build.
- Harness: `SurlOnLoopback.cs`, `PinnedUpstreamCurl.cs`, `AccountsFile.cs`,
  `TestCertificateAuthority.cs`.
- Any disagreement with the pinned build is fixed in the library at fault through a new task filed by
  `task-planner`, never by changing the expected result (ADR-0003); list them in the Log.

## Acceptance criteria

- [x] Integration tests exist for every case of BL-284's ADR and pass on Windows with the pinned build
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] Where the platform's pin lists no `ldap`, the tests report Inconclusive with the pin named.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a socket.

## Notes

- Stewart approved lanes filing follow-up tasks beyond the plan (2026-09-30, "yes extra tasks"): a
  disagreement this task finds becomes its own task rather than widening this one.

- **What was built (2026-09-30).** `UpstreamCurlSearchesSurlOverLdapTests` (Integration): every
  case of ADR-0072 decision 10, 21 tests, run against a live in-process surl serving a temporary
  `--directory` whose `.surl/ldap/directory.ldif` holds the ADR's entries (built in code, not a
  file), plus the in-memory case. All 21 pass with the pinned win-x64 reference build;
  `dotnet test Surl.Conformance.UnitTests --filter "FullyQualifiedName~Surl.Conformance"`: 563
  passed, 6 skipped (other platforms' `OSCondition` cases), 0 failed.
- **Which build, and Inconclusive.** Added `PinnedUpstreamCurl.RunReferenceForProtocolAsync`
  beside `RunForProtocolAsync`: it runs only the platform's *reference* build and is Inconclusive
  naming that pin (path and version) when the pin lists no `ldap`. `RunForProtocolAsync` would pick
  ADR-0076's OpenLDAP supplementary build on Linux, which answers differently (`-k` works on
  `ldaps`) and is BL-312's to prove. Default taken: the reference build, because these cases are
  `WinLDAP`'s.
- **Measured against the ADR** (no disagreement with the server; nothing filed against a library):
  - `--ntlm` with `ntlm` not accepted: exit 38 with `Authentication Method Not Supported` (the ADR
    wrote `Auth Method Not Supported` by decision). The test pins the measured wording.
  - The three sealed binds (`--ntlm` Sicily, `--negotiate` `GSS-SPNEGO`, `--digest` `DIGEST-MD5`
    `auth-conf`) exit 0 with the base entry: the security layers work with `WinLDAP`.
  - `?cn,mail?one` also returns `ou=many,dc=example,dc=com`, a one-level child; curl prints its
    `DN:` line alone (`DN: ou=many,dc=example,dc=com\n`), pinned in the test.
  - The `ou=many` entries need an `objectClass`: `WinLDAP`'s default filter is `(ObjectClass=*)`,
    and surl rightly returned 0 entries for entries without one.
- ADR-0072 could not be amended here (BL-327 holds `Documentation/Planning/Decisions`): filed
  BL-344 to write these results into decision 10.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The pinned WinLDAP upstream curl completes every ADR-0072 decision 10 case against a live surl over ldap and ldaps, inconclusive with the pin named where the reference pin lists no ldap
