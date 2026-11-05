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
completed:
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

- [ ] Named tests show:
      - a zone file with each served type loaded and each record answered;
      - a malformed line reported with its line number and the zone refused;
      - an existing name with no record of the asked type gets NODATA with the SOA;
      - a missing name gets NXDOMAIN with the SOA;
      - an in-zone CNAME followed to its A record;
      - an out-of-zone name gets REFUSED with `RA = 0`;
      - a mixed-case question answered with its own case;
      - a wildcard answered if the ADR serves wildcards.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
