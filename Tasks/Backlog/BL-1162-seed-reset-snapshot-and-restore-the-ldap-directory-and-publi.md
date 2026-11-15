---
id: BL-1162
title: Seed, reset, snapshot and restore the LDAP directory and publish its changes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1157]
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests]
requirement: FR-061
created: 2026-11-14
completed:
---
# BL-1162 — Seed, reset, snapshot and restore the LDAP directory and publish its changes

## Goal

The LDAP directory can be seeded with LDIF content records while the server runs, reset to what it held at start, snapshotted and restored, and publishes a `StoreChangedEvent` for each, so an in-memory LDAP stand-in can serve the entries a test needs.

## Context

- `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapDirectory.cs` is built once with its entries (`Entries`, `entriesByNormalForm`, limits `DefaultMaxEntries`, `DefaultMaxTotalBytes`, `MaxValueBytes`) and is read-only; `LdapDirectoryFile.cs` reads RFC 2849 LDIF content records from `<path>/.surl/ldap/directory.ldif` (ADR-0072 decision 1). Without `--directory` the directory is empty.
- Reuse the LDIF reader `LdapDirectoryFile` uses for the seed input, so a seed is refused with the same `line <n>: <what>` reasons a file is. Searches running while the directory changes must see either the old or the new contents, never a mix (one lock or an immutable swap).
- Operations, events (`StoreKind` from BL-1157), bounds and the on-disk rule are BL-1151's ADR. A seed never consults the fault plan.

## Acceptance criteria

- [ ] Named tests: an entry seeded from LDIF text is found by an `LdapSearchRequest` at base, one-level and subtree scope; seeding an existing DN, an entry whose parent is missing (if the ADR requires parents), or text that is not LDIF is refused with the reader's message and changes nothing.
- [ ] Named tests: reset returns the directory to the entries it was built with; a snapshot taken, then changed, then restored gives back the snapshot's entries; a snapshot from another directory is refused; the entry and byte limits apply to seeds.
- [ ] Named tests: seed, reset and restore each publish the `StoreChangedEvent` the ADR decides.
- [ ] Every existing LDAP test passes unchanged, and `dotnet build -warnaserror` is clean with the fast tests green.
- [ ] `Surl.Protocol.Ldap.UnitLibrary` keeps 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-14: Created.
