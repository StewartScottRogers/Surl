---
id: BL-344
title: Record BL-311's measured WinLDAP results in ADR-0072 decision 10
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-311]
touches: [Documentation/Planning/Decisions/ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-344 — Record BL-311's measured WinLDAP results in ADR-0072 decision 10

## Goal

ADR-0072 decision 10 states what BL-311 measured with the pinned Windows build against a live surl,
where it differs from what the ADR wrote by decision rather than measurement.

## Context

- ADR-0072 decision 10 asks BL-311 to record the measured wording "against this ADR rather than
  changing the server". BL-311 could not edit the ADR: BL-327 (in Doing) held
  `Documentation/Planning/Decisions`.
- The proof: `Surl.Conformance.UnitTests/UpstreamCurlSearchesSurlOverLdapTests.cs`; BL-311's
  `Notes` list the results.

## Acceptance criteria

- [ ] ADR-0072 decision 10's table says the `--ntlm` refusal without `ntlm` accepted reads
      `Authentication Method Not Supported` (measured), not `Auth Method Not Supported`.
- [ ] Decision 10 says the three sealed binds (`--ntlm`, `--negotiate`, `--digest`) were measured
      to exit 0 with the base entry, and that the `?cn,mail?one` case also prints
      `DN: ou=many,dc=example,dc=com` (a child with neither attribute: its DN line alone).
- [ ] Decision 10 says the `ou=many` entries carry `objectClass` because `WinLDAP`'s default filter
      is `(ObjectClass=*)`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
