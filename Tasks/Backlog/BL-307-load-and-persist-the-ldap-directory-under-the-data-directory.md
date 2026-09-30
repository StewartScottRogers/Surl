---
id: BL-307
title: Load and persist the LDAP directory under the data directory in Surl.Protocol.Ldap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-306]
touches: [Surl.Protocol.Ldap.UnitLibrary, Surl.Protocol.Ldap.UnitTests]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-307 — Load and persist the LDAP directory under the data directory in Surl.Protocol.Ldap

## Goal

The LDAP directory loads from, and (if BL-284's ADR says it changes at run time) saves to, the byte
format BL-284's ADR pins under `<path>/.surl/` through `IContentFileSystem`, and starts as the ADR
says in memory; a malformed file is reported so `surl` can end with `CouldNotReadFile` (37) before any
listener binds.

## Context

- Decisions: BL-284's ADR (the file name under `<path>/.surl/`, its format - e.g. LDIF, RFC 2849 -
  what the in-memory mode starts with, the malformed-file text); ADR-0031 (service state under
  `<path>/.surl/<service>/`, never served; decision 7's start-up order: lock, load service state,
  bind); ADR-0050 decision 7 and `Surl.MailStore`'s `MailboxStore.LoadAsync` and
  `MailStoreLoadException` as the pattern for a load that fails with a file path and a message.
- Code: BL-306's directory; `Surl.Content.UnitLibrary/IContentFileSystem.cs`,
  `InMemoryContentFileSystem.cs` for tests. The csproj gains the `Surl.Content.UnitLibrary`
  reference (ADR-0002's table allows it) unless an earlier LDAP task already added it.
- Writes, if any, go through a temporary name and `MoveFileReplacing`, as ADR-0031 decision 6 and
  the mail store do; a failed write is noted, not fatal.

## Acceptance criteria

- [ ] Tests in `Surl.Protocol.Ldap.UnitTests` load a fixture file in the ADR's format into the
      directory over `InMemoryContentFileSystem` and search it; a round trip (if the directory is
      saved) gives the same bytes; each malformed case the ADR names throws the load exception with
      the file path and the ADR's text; a directory past the ADR's bounds is refused.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ldap.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
