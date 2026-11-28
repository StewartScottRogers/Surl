---
id: BL-1971
title: Name the FTP AUTH TLS tests after a TLS upgrade being available, not a certificate
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests/FtpTlsTests.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1971 — Name the FTP AUTH TLS tests after a TLS upgrade being available, not a certificate

## Goal

The names in `FtpTlsTests` say what the tests vary: whether a TLS upgrade is available.

## Context

Found while working BL-1952 (2026-11-27), which did the same for LDAP. `FtpProtocolServer` is told only
`isTlsUpgradeAvailable` (a certificate or TLS-SRP; ADR-0122 decision 5, name per ADR-0069), never a certificate. In
`Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests/FtpTlsTests.cs`, lines 47, 72 and 163 (approximate):
`Auth_WithACertificate_Answers234AndUpgradesWithNoNote`,
`Auth_WithNoCertificate_Answers534AndPassStaysRefusedAsPlainText`,
`Feat_WithACertificate_AlsoListsAuthTlsPbszAndProt`.

Use the LDAP shape: `...WithTlsUpgradeAvailable_...`, `...WithTlsUpgradeUnavailable_...`. Names only; no store, event
or fault is involved.

## Acceptance criteria

- [ ] No test method name or `DisplayName` in `FtpTlsTests.cs` says "Certificate" or "certificate"; each renamed test says TLS upgrade available or unavailable.
- [ ] The diff changes only those names.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
