---
id: BL-1944
title: Name Surl.Composition as the LDAP server's host in the LDAP notes and fix the StartTLS test summary
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests/LdapProtocolServerStartTlsTests.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1944 — Name Surl.Composition as the LDAP server's host in the LDAP notes and fix the StartTLS test summary

## Goal

The LDAP notes name `Surl.Composition` as what registers `ldaps` and reports a directory load failure, and the `StartTLS` test summary says what the tests vary: whether a TLS upgrade is available.

## Context

Found while working BL-1931 (2026-11-27), read from source. Text only; no code or behaviour change. Line numbers are as of that date and approximate.

- `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md` about line 47 says `Surl.Console` registers the `ldaps` wrapper, and about line 67 that `Surl.Console` turns `LdapDirectoryLoadException` into `CouldNotReadFile` (37). Both happen in `Application/Surl.Composition.UnitLibrary` (`ProtocolServerRegistration.cs`, `SurlServerComposition.LoadLdapServerAsync`).
- `Protocols/Messaging/Surl.Protocol.Ldap.UnitTests/LdapProtocolServerStartTlsTests.cs` line 9: the `<summary>` says "`StartTLS` with and without a certificate". The server is told only `isTlsUpgradeAvailable` (a certificate or TLS-SRP).

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] Both `CLAUDE.md` sentences name `Surl.Composition`, checked against its source.
- [ ] The test class summary says `StartTLS` with and without a TLS upgrade available.
- [ ] The `.cs` diff changes only `///` comment lines.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
