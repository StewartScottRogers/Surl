---
id: BL-1357
title: Cover DnsZoneSet.ZoneFor and the label-dot escape in DnsZoneFileWriter
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1357 — Cover DnsZoneSet.ZoneFor and the label-dot escape in DnsZoneFileWriter

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.DnsQuery.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 99.96% line, 99.89% branch, 2
  failing members, in `Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary/`:
  - `DnsZoneSet.ZoneFor(DnsName)` - `DnsZoneSet.cs:242`, 0% line. `DnsZoneSetSeedResetSnapshotTests` line 222 calls
    `ZoneFor` on a view, not on the `DnsZoneSet` itself, so the set's own member never runs.
  - `DnsZoneFileWriter.EscapedText(ReadOnlySpan<byte>, bool)` - `DnsZoneFileWriter.cs:63`, 96.43% branch; line 71/72 is
    19 of 20 - most likely `isLabel && value == '.'` with a dot inside a label (a label holding `.` written as `\.`).

## Acceptance criteria

- [ ] A named `Surl.Extended.Protocol.DnsQuery.UnitTests` test calls `DnsZoneSet.ZoneFor` for a name under a seeded
      zone and for a name under none, asserting the zone and `null`.
- [ ] A named test writes a zone whose owner name has a label containing a `.` byte and asserts it is written `\.`,
      and that the same byte in a character-string is written unescaped (lines 71 and 72, every outcome).
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.DnsQuery.UnitLibrary` reports `100 | 100` with 0 failing
      members, and the fast tests are green.

## Notes

- Tests only (test-writer). Escaping per RFC 1035 section 5.1.

## Log

- 2026-11-17: Created.
