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
completed: 2026-10-01
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

- [x] ADR-0072 decision 10's table says the `--ntlm` refusal without `ntlm` accepted reads
      `Authentication Method Not Supported` (measured), not `Auth Method Not Supported`.
- [x] Decision 10 says the three sealed binds (`--ntlm`, `--negotiate`, `--digest`) were measured
      to exit 0 with the base entry, and that the `?cn,mail?one` case also prints
      `DN: ou=many,dc=example,dc=com` (a child with neither attribute: its DN line alone).
- [x] Decision 10 says the `ou=many` entries carry `objectClass` because `WinLDAP`'s default filter
      is `(ObjectClass=*)`.

## Notes

- Done directly rather than through `align-and-document`: one file, three facts already proven by
  BL-311's `UpstreamCurlSearchesSurlOverLdapTests` and its Notes. Decision 10's table now holds the
  measured `Authentication Method Not Supported` and the `ou=many` DN line; a paragraph after it
  says which four rows were first written by decision and what was measured; the header's
  `Amended` line names BL-344. No `.cs` changed; build clean, fast tests green.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. ADR-0072 decision 10 records BL-311's measured WinLDAP results: the refusal wording, the three sealed binds, the ou=many DN line and why ou=many entries carry objectClass
