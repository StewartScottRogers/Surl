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
completed: 2026-09-30
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

- [x] Tests in `Surl.Protocol.Ldap.UnitTests` load a fixture file in the ADR's format into the
      directory over `InMemoryContentFileSystem` and search it; a round trip (if the directory is
      saved) gives the same bytes; each malformed case the ADR names throws the load exception with
      the file path and the ADR's text; a directory past the ADR's bounds is refused.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ldap.UnitLibrary`.

## Notes

- **No persistence.** ADR-0072 decision 1 makes the directory read-only while serving, never
  written, so there is no save path and no round trip; the round-trip half of the first
  criterion does not apply. A missing file and in-memory mode are an empty directory.
- **What was built.** Public `LdapDirectoryFile` (`directory.ldif` in the state folder it is
  given, read through `IContentFileSystem`), public `LdapDirectoryLoadException` (`FilePath`,
  message `line <n>: <what>` or the read failure's message, as `MailStoreLoadException`), public
  `LdapProtocolServer.LoadAsync(file, policy, timeProvider)` for BL-310 to call before binding.
  Internal `LdifReader`, `LdifLine`, `LdifRecord`, `LdifFormatException`, `LdifFaultText` (the
  ADR's fourteen texts). Faults `LdapDirectory` finds are mapped to the record's `dn` line. The
  csproj gained the `Surl.Content.UnitLibrary` reference (ADR-0002's table allows it).
- **Choices inside the ADR's words** (no new ADR: each is a reading of decision 1's text and
  RFC 2849, not a new behaviour):
  - A leading UTF-8 byte order mark is `line 1: not UTF-8` (the ADR says "without a byte order
    mark", and that is its nearest text).
  - SAFE-STRING is RFC 2849's: ASCII only, so a non-ASCII value or DN must be base64
    (`a value that is not a SAFE-STRING` otherwise); the value keeps its trailing spaces.
  - An empty `dn:` (the root DSE's, which the directory refuses) reads `a record without dn`.
  - `version:` is recognised only on the file's first logical line, case-insensitively, and
    must be exactly `1` after its spaces; elsewhere it is an ordinary first line, so a later
    record starting `version:` is `a record without dn`.
  - A `changetype` line anywhere after `dn` makes it `a changetype record`.
  - Base64 is strict: only the base64 alphabet and `=`, no inner or trailing whitespace.
  - A description is RFC 4512's: a name `[A-Za-z][A-Za-z0-9-]*` or a numeric OID without leading
    zeros, then `;`-options of letters, digits and hyphens. Repeats of one description, compared
    ignoring case, add values to the first attribute in file order.
- **Measured:** `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ldap.UnitLibrary` - 100% line,
  100% branch, 297 members, 0 failing, worst CRAP 10. LDAP tests 447, with this task's
  new `LdifReaderTests` and `LdapDirectoryFileTests`.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The LDAP directory loads from <path>/.surl/ldap/directory.ldif (RFC 2849 LDIF) through IContentFileSystem; a malformed or unreadable file throws LdapDirectoryLoadException with the path and ADR-0072's line text
