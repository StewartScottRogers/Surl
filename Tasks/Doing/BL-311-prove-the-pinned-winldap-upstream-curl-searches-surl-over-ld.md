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
completed:
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

- [ ] Integration tests exist for every case of BL-284's ADR and pass on Windows with the pinned build
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] Where the platform's pin lists no `ldap`, the tests report Inconclusive with the pin named.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a socket.

## Notes

- Stewart approved lanes filing follow-up tasks beyond the plan (2026-09-30, "yes extra tasks"): a
  disagreement this task finds becomes its own task rather than widening this one.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
