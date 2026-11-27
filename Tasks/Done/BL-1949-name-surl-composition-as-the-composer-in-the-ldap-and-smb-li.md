---
id: BL-1949
title: Name Surl.Composition as the composer in the LDAP and SMB library notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1931]
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md, Protocols/FileTransfer/Surl.Protocol.Smb.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1949 — Name Surl.Composition as the composer in the LDAP and SMB library notes

## Goal

The LDAP and SMB libraries' `CLAUDE.md` files name `Surl.Composition`, not `Surl.Console`, as the project that
registers each server.

## Context

Found while working BL-1929 (2026-11-27), read from source. Project-notes text only. Line numbers are as of that date and
approximate. BL-1931 corrects the `StartTLS` certificate sentences of the LDAP notes; this task leaves them as that task
writes them.

What the code does (`Surl.Composition`):

- `ProtocolServerRegistrations.Original` registers `ldaps` with `ProtocolServerRegistration.ImplicitTls("ldaps",
  "ldap")`, an `ImplicitTlsSchemeServer` over the `ldap` server.
- `SurlServerComposition.LoadLdapServerAsync` catches `LdapDirectoryLoadException` and returns the `(37)` message, and
  `SurlServerComposition.Start.cs` refuses the start with `SurlExitCode.CouldNotReadFile`.
- `ProtocolServerRegistrations.Original` registers `SmbProtocolServer` once, for `smb` and `smbs`.

The stale sentences:

- `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md`, about lines 45-46: "`Surl.Console` registers the
  server a second time behind `ImplicitTlsSchemeServer`".
- The same file, about lines 65-66: "which `Surl.Console` turns into `CouldNotReadFile` (37)".
- `Protocols/FileTransfer/Surl.Protocol.Smb.UnitLibrary/CLAUDE.md`, about lines 17-18: "`Surl.Console` registers it
  once, with no `ImplicitTlsSchemeServer`".

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] The LDAP `CLAUDE.md` says `Surl.Composition` registers `ldaps` as an `ImplicitTlsSchemeServer` over the `ldap` server.
- [x] The LDAP `CLAUDE.md` says `Surl.Composition` turns `LdapDirectoryLoadException` into `CouldNotReadFile` (37) before any listener binds.
- [x] The SMB `CLAUDE.md` says `Surl.Composition` registers the server once for `smb` and `smbs`, with no `ImplicitTlsSchemeServer`.
- [x] Only those two `CLAUDE.md` files change.

## Notes

- Both LDAP sentences were already correct when this task ran: BL-1944 (`aba82b1d4`) had fixed them. Checked against
  `ProtocolServerRegistrations.cs`, `SurlServerComposition.LoadLdapServerAsync` and `SurlServerComposition.Start.cs`
  (the refusal returns before `ComposeServing`). The LDAP file is unchanged.
- The SMB sentence now names `Surl.Composition`'s `ProtocolServerRegistrations.Original`. It is the only file outside
  the board that this task changes.
- Verified 2026-11-27: `dotnet build` with 0 warnings and 0 errors; every fast test assembly passed, 0 failed.
- Filed for the stale statements found outside this task's files: BL-1962 to BL-1961.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The LDAP and SMB library notes name Surl.Composition as the project that registers each server
