---
id: BL-1644
title: Cover LdapBindBytesRedactor.FindSaslSecret's truncated-credentials branch
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-1644 — Cover LdapBindBytesRedactor.FindSaslSecret's truncated-credentials branch

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Protocol.Ldap.UnitLibrary` reports 100% line and 100% branch with no failing
member.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Surl.Protocol.Ldap 100% line,
  99.89% branch, 1 failing member.
- `LdapBindBytesRedactor.FindSaslSecret(ReadOnlySpan<byte>, Tlv)`,
  `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapBindBytesRedactor.cs:207`, 87.5% branch. Line 222
  (`credentials.IsWhole ? SecretOf(credentials) : (credentials.ValueStart, message.Length, false)`) is 1 of 2: no test
  gives a SASL `PLAIN` bind whose mechanism is whole but whose credentials OCTET STRING is cut off before its declared
  length.
- The simple-bind counterpart of this case is already tested. BL-1610 (commit 58b796abc) set how withheld credentials are
  logged: BER length shown as 4, then `****`, per ADR-0182 decision 1.

## Acceptance criteria

- [ ] A named `Surl.Protocol.Ldap.UnitTests` test redacts a BindRequest with SASL mechanism `PLAIN` whose credentials
      octet string declares more bytes than the message holds, and asserts the logged bytes withhold everything from
      the credentials' value to the end of the message.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Protocol.Ldap.UnitLibrary` reports `100 | 100`
      with 0 failing members, and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Tests only (test-writer). BER bytes are written from RFC 4511 section 4.2.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
