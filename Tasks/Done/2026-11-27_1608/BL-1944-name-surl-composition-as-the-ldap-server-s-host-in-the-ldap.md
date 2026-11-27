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
completed: 2026-11-27
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

- [x] Both `CLAUDE.md` sentences name `Surl.Composition`, checked against its source.
- [x] The test class summary says `StartTLS` with and without a TLS upgrade available.
- [x] The `.cs` diff changes only `///` comment lines.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Checked against source: `ProtocolServerRegistrations.Original` has
  `ProtocolServerRegistration.ImplicitTls("ldaps", "ldap")`; `SurlServerComposition.LoadLdapServerAsync` catches
  `LdapDirectoryLoadException` and returns the `(37)` message, and `SurlServerComposition.Start.cs` refuses the start
  with `SurlExitCode.CouldNotReadFile`. The notes now say each of these.
- Made directly rather than through `align-and-document`: two sentences and one comment.
- Verify: build `-warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 0 failed.
  No Integration run: comments and notes only.
- BL-1949 (Backlog) also lists the two LDAP sentences; its two LDAP criteria are now true, leaving the SMB one.
- Filed BL-1952: the same test file's method names and display names still say "certificate".

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The LDAP notes name Surl.Composition as the ldaps registrar and directory-load reporter; the StartTLS test summary says TLS upgrade available
