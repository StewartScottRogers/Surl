---
id: BL-306
title: Keep LDAP directory entries and evaluate search filters in Surl.Protocol.Ldap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-284, BL-289]
touches: [Surl.Protocol.Ldap.UnitLibrary, Surl.Protocol.Ldap.UnitTests]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-306 — Keep LDAP directory entries and evaluate search filters in Surl.Protocol.Ldap

## Goal

`Surl.Protocol.Ldap` holds the directory BL-284's ADR decides - entries with DNs and attributes, the
root DSE, bounds - and answers a search over it: base, one-level and subtree scopes, every RFC 4511
filter item with the ADR's matching rules, the attribute selection, the size limit, as internal types
with no transport.

## Context

- Decision: BL-284's ADR (the entry model, DN parsing and normalisation per RFC 4514, the matching
  rule for each attribute and filter item per RFC 4517, `approxMatch` and `extensibleMatch` answers,
  the root DSE's attributes, the bounds on entries and results, safe for concurrent sessions).
- Codec: BL-289's `SearchRequest` and `Filter` types are the input; the output is the entries a search
  returns and the `LDAPResult` code (`noSuchObject` with matched DN, `sizeLimitExceeded`, and so on)
  the ADR decides.
- Access control is not here: which bound identity may read what is BL-308's, applied through what
  this task exposes.
- Constraints: internal types; complexity at most 10 per method (filters recurse - keep each
  evaluator small); no package.

## Acceptance criteria

- [ ] Tests in `Surl.Protocol.Ldap.UnitTests` search a directory of fixture entries with each scope
      and each filter choice (`and`, `or`, `not`, equality, substrings, `>=`, `<=`, presence, approx,
      extensible) and return exactly the ADR's entries and attributes, including `*`, `1.1` and named
      attribute lists (RFC 4511 section 4.5.1.8), and `typesOnly`.
- [ ] Tests show DN normalisation (case, spacing, escaped characters of RFC 4514) making equal DNs
      match, a base that does not exist answered with the ADR's code, and each bound enforced.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ldap.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
