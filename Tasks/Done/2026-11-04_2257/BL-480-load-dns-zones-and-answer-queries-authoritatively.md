---
id: BL-480
title: Load DNS zones and answer queries authoritatively
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-479]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-480 — Load DNS zones and answer queries authoritatively

## Goal

The DNS server loads zones from RFC 1035 master files as BL-478's ADR decides. It answers a query with `AA`
set and one of: the matching records, NODATA, NXDOMAIN with the SOA, an in-zone CNAME chain, or REFUSED outside
its zones.

## Context

- Decision: BL-478's ADR, decisions 1 and 2.
- RFC 1035 section 5 (master files: `$ORIGIN`, `$TTL`, `@`, relative names, parentheses, comments; and the
  RFC 3597 `\#` form for unknown types), section 4.3.2 (the lookup algorithm for an authoritative server), and
  RFC 2308 section 3 (negative answers carry the SOA with the minimum TTL). Names are matched case-insensitively
  (RFC 4343), with the question's case kept.
- Files are read through `Surl.Content`'s `IContentFileSystem` (tests use a `UnitTest`-prefixed fake, as
  `.claude/rules/testing.md` requires), not `System.IO` directly.

## Acceptance criteria

- [x] Named tests show:
      - a zone file with each served type loaded and each record answered;
      - a malformed line reported with its line number and the zone refused;
      - an existing name with no record of the asked type gets NODATA with the SOA;
      - a missing name gets NXDOMAIN with the SOA;
      - an in-zone CNAME followed to its A record;
      - an out-of-zone name gets REFUSED with `RA = 0`;
      - a mixed-case question answered with its own case;
      - a wildcard answered if the ADR serves wildcards.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Tests for each criterion: `DnsAuthoritativeLookupTests.Answer_EachServedTypeInTheZoneFile_AnswersItsRecord`,
  `DnsZoneSetTests.Add_MalformedLine_RefusesTheZoneNamingTheLine` (70 rows) and `DnsZoneFolderTests.LoadAsync_MalformedZoneFile_ThrowsNamingTheFileAndTheLine`,
  `Answer_NameWithoutTheType_AnswersNoDataWithTheSoaAtItsNegativeTtl`, `Answer_MissingName_AnswersNxDomainWithTheSoa`,
  `Answer_InZoneCanonicalName_FollowsItToTheARecord`, `Answer_NameOutsideEveryZone_AnswersRefusedWithRecursionUnavailable`,
  `Answer_MixedCaseQuestion_AnswersWithTheQuestionsOwnCase`, `Answer_NameBelowAWildcardsParent_AnswersTheWildcardAsTheName`
  (ADR-0092 serves wildcards). Fast tests: 46 test projects green; DnsQuery 204 tests; `Measure-CodeQuality.ps1`
  gives the library 100% line, 100% branch, worst CRAP 10, 0 failing members.
- Reading the files: the task context says files are read through `IContentFileSystem`; ADR-0092 decision 1 says
  `Surl.Console` lists and reads them. Both hold: `DnsZoneFolder` (in this library, as `LdapDirectoryFile` is in
  LDAP's) lists and reads `*.zone` through the seam, and BL-485 only constructs it over `<data>/.surl/dns`. This adds
  a reference to `Surl.Content.UnitLibrary`, a horizontal library ADR-0002 decision 3 already allows a protocol
  server; the csproj is inside this task's touches.
- Choices within ADR-0092 (no new ADR needed; the ADR left the fault list to this task):
  - Fault texts pinned: `a byte order mark`, `not UTF-8`, `an unclosed quote`, `a ')' without '('`,
    `a '(' without ')'`, `$INCLUDE is not served`, `$ORIGIN|$TTL takes one value`, `an unknown directive <d>`,
    `a bad TTL <t>`, `a TTL past 2147483647` (RFC 2181 section 8), `no type`, `an owner outside the zone`,
    `a class other than IN`, `an unknown type <t>`, `a meta-type <T> as a record`, `a quoted "<x>" where a word
    belongs`, `bad RDATA for <type>`, `a bad name <n>`, `a blank owner with no owner before it`,
    `no TTL, and no $TTL before it`, `a second SOA`, `an SOA below the apex`, `no SOA at the apex`,
    `a CNAME beside other data`, `a second CNAME`, `records of one set with different TTLs`, `a record written twice`,
    `a wildcard NS`, `an RDATA past 65535 bytes`, `a second zone for <apex>`, `a file name not ending in .zone`,
    `a file name that is not a domain name`, `past the zones' bounds`.
  - The faults no line holds (no SOA, second zone, file name, bounds) are reported without `line <n>:`; inventing a
    line would point the operator at the wrong place.
  - TTLs are decimal seconds only (RFC 1035 section 5.1); BIND's `1h` unit form is refused, not guessed at. A record
    without a TTL takes the last `$TTL`, else the last TTL written (RFC 1035 section 5.1's rule).
  - Mnemonics read: the ten served types and the four meta-types (refused); every other type is `TYPE<n>` with `\#`.
  - Duplicate records are compared by owner (case folded), type and RDATA bytes as written.
  - The authority section drops the apex NS when the answer already holds it, and the additional section drops an
    address already answered (RFC 2181 section 5.1's spirit: no record twice in one message). A chain that runs
    into a delegation answers its CNAMEs with AA = 1 and the referral in the authority section.
  - AXFR and IXFR questions are not special-cased in the lookup (they get NODATA from it); BL-482 owns them.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. The DNS server loads RFC 1035 zone files and answers authoritatively: records, NODATA, NXDOMAIN, CNAME chains, wildcards, referrals, REFUSED
