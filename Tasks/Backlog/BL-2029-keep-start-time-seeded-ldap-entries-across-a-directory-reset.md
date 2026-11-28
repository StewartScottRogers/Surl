---
id: BL-2029
title: Keep start-time seeded LDAP entries across a directory reset
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1987]
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests]
requirement: FR-061
created: 2026-11-27
completed:
---
# BL-2029 — Keep start-time seeded LDAP entries across a directory reset

## Goal

The LDAP server can seed entries as part of its directory's configured state, so a reset returns to the file's entries plus those entries.

## Context

- ADR-0158 decision 4: a directory reset returns to the entries loaded from `--directory`'s file, then the host's start-time entries; with neither, empty. Decision 3: start-time seeds go through the same seed operation (all or nothing, `Seeded` per entry, no fault plan).
- Today `LdapDirectory.Reset` (`Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapDirectory.cs`) returns only to the `configured` entries its constructor was given, so every seed is dropped.
- Add `bool keepOnReset = false` to `LdapProtocolServer.SeedDirectoryEntries(string ldif)` and `LdapDirectory.SeedEntries`, the name `DnsZoneSet.SeedZone` already uses. A kept seed adds the entries to both the live contents and the configured contents. Both resulting sets must be valid under the existing rules (parent present, no second DN, bounds): a kept entry whose parent exists only through an unkept seed is refused with `InvalidOperationException` in the existing `line <n>: <what>` form, and a refusal changes neither set and publishes nothing.
- Snapshot and restore do not change the configured contents.
- Composition calls the flag in BL-2030. BL-1987 rewrites the library `CLAUDE.md` paragraph saying `ResetDirectory` returns to the entries the server was built with and drops seeded ones; update it, and the XML comments of `ResetDirectory`, `Reset`, `SeedEntries` and the `entries` constructor parameter.
- Stand-in: this is the LDAP directory store. It publishes `Seeded`, `Reset` and `Restored`; a test seeds and resets it; a seed never consults the fault plan. LDAP operations still cannot change it, so no bytes sent to curl change.

## Acceptance criteria

- [ ] Named tests in `Surl.Protocol.Ldap.UnitTests` (beside `LdapDirectorySeedTests`): after a kept seed and an unkept seed, `Reset` leaves the constructor's entries plus the kept ones, and not the unkept ones; a second reset gives the same.
- [ ] Named tests: a kept seed whose parent is an unkept seeded entry, a second DN and a bound passed are each refused, leaving the live entries and what reset returns to unchanged, with no event published.
- [ ] Named test: a kept seed publishes one `Seeded` event per entry, and `Reset` publishes one `Reset` event and no `Seeded` event.
- [ ] Named test: after `Restore` of a snapshot taken before the kept seed, `Reset` still returns to the set holding the kept entries.
- [ ] The XML comments and the library `CLAUDE.md` state that reset returns to the configured entries plus kept seeds.
- [ ] `dotnet build Protocols/Messaging/Surl.Protocol.Ldap.UnitTests -warnaserror` is clean, the fast tests pass without `TestCategory=Integration`, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
