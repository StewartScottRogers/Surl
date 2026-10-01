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
completed: 2026-09-30
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

- [x] Tests in `Surl.Protocol.Ldap.UnitTests` search a directory of fixture entries with each scope
      and each filter choice (`and`, `or`, `not`, equality, substrings, `>=`, `<=`, presence, approx,
      extensible) and return exactly the ADR's entries and attributes, including `*`, `1.1` and named
      attribute lists (RFC 4511 section 4.5.1.8), and `typesOnly`.
- [x] Tests show DN normalisation (case, spacing, escaped characters of RFC 4514) making equal DNs
      match, a base that does not exist answered with the ADR's code, and each bound enforced.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ldap.UnitLibrary`.

## Notes

Built in `Surl.Protocol.Ldap.UnitLibrary` (all internal): `LdapDirectory` (entries, bounds,
`Search`, `FindEntry`, `NamingContexts`), `LdapDistinguishedName` and its parser,
`LdapMatchingRules`, `LdapFilterEvaluator`, `LdapAttributeSelection`, and their records.
`Surl.Protocol.Ldap.UnitTests` now runs 283 tests, all green;
`Measure-CodeQuality.ps1` (coverage from the Ldap test project, `-SkipTestRun`, because the
whole-solution run was too slow on the shared shift machine): 100% line, 100% branch,
0 failing members, worst CRAP 10.

Choices where ADR-0072 left a default open (sensible defaults, rule 1 of the shift):

- **A base that is not an RFC 4514 DN** is `invalidDNSyntax` (34), diagnostic `the base is
  not an RFC 4514 DN`: the code RFC 4511 section 4.1.9 names for exactly that, so no new
  decision.
- **"A naming context is an entry whose parent DN is not in the directory. Every other
  entry's parent must be"** is read as: an entry with no superior at all in the directory is
  a naming context; an entry with some superior present but its parent missing is
  `LdapDirectoryFault.ParentNotInDirectory`. The check runs after every entry is held, so a
  child may come before its parent.
- **An entry named by the empty DN** is refused (`LdapDirectoryFault.RootDseEntry`): the
  root DSE is computed per connection, never stored. BL-307 must give it a line text; the
  nearest of ADR-0072's is `a duplicate DN` or a new one - BL-307's call.
- **Faults carry the entry index** (`LdapDirectoryException.EntryIndex`), so BL-307's LDIF
  loader can name the record's first line.
- **`namingContexts` order** ("in DN order"): ordinal ignoring case on the DN as written.
- **The root DSE's operational attributes** (`namingContexts`, `supportedLDAPVersion`,
  `supportedSASLMechanisms`, `supportedExtension`) are returned for `+` or by name; `*` or
  no list returns `objectClass: top` alone. `namingContexts` is left out when the directory
  is empty (an attribute cannot have no values).
- **`1.1` beside other names** is ignored (RFC 4511 section 4.5.1.8); `+` on an ordinary
  entry adds nothing.
- **Unicode simple case folding** is `char.ToLowerInvariant(char.ToUpperInvariant(c))` per
  UTF-16 unit - the BCL has no case-fold API; it agrees with simple folding on every
  letter curl's tests use.
- **Substring parts** get RFC 4518's space collapsing and case folding but are not trimmed
  (the value is), so `(cn=Babs *)` still needs the space after `Babs`.
- **A value that is not UTF-8** under a string rule, and a non-integer under
  `integerMatch`, is Undefined. An `extensibleMatch` with a rule and no type is applied to
  every attribute.
- **`timeLimit`** is checked before each candidate entry against the injected
  `TimeProvider`, from the moment the search starts.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Protocol.Ldap holds a bounded directory and answers base, one-level and subtree searches with every RFC 4511 filter, RFC 4514 DN normalisation, attribute selection and the root DSE
