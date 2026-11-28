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
completed: 2026-11-27
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

- [x] Named tests in `Surl.Protocol.Ldap.UnitTests` (beside `LdapDirectorySeedTests`): after a kept seed and an unkept seed, `Reset` leaves the constructor's entries plus the kept ones, and not the unkept ones; a second reset gives the same.
- [x] Named tests: a kept seed whose parent is an unkept seeded entry, a second DN and a bound passed are each refused, leaving the live entries and what reset returns to unchanged, with no event published.
- [x] Named test: a kept seed publishes one `Seeded` event per entry, and `Reset` publishes one `Reset` event and no `Seeded` event.
- [x] Named test: after `Restore` of a snapshot taken before the kept seed, `Reset` still returns to the set holding the kept entries.
- [x] The XML comments and the library `CLAUDE.md` state that reset returns to the configured entries plus kept seeds.
- [x] `dotnet build Protocols/Messaging/Surl.Protocol.Ldap.UnitTests -warnaserror` is clean, the fast tests pass without `TestCategory=Integration`, and the library keeps 100% line and branch coverage.

## Notes

- Plan: `LdapDirectory.configured` becomes replaceable under the change lock. A kept seed builds the new live set, then the new
  configured set, and assigns both only when both are valid, so a refusal changes neither.
- Choice: the directory's own rules accept an entry with no superior at all as a naming context. A kept entry that has a parent
  among the live entries but no superior in the configured set (for example a child of an unkept seeded naming context) is
  refused too, with `line <n>: a parent DN that is not in the file`, as this task's Context asks. No ADR: the task and ADR-0158
  decisions 3 and 4 already specify it.
- The reverse case is allowed: after a restore drops a kept parent from the live entries, the rules for each set alone apply.
- Pipeline: the task fixes the design, so plan, tests, implementation and documentation were done in the session; review was
  delegated to `code-reviewer` (no Must fix; its three Optional wording and test tidy-ups applied). Conformance skipped: no
  option, exit code or wire byte changes.
- Evidence: solution build clean, `dotnet format --verify-no-changes` clean, fast tests 30452 passed, 0 failed, 3 skipped (before
  the review's wording tidy-ups; after them the build, format check and 589 LDAP tests pass). `Measure-CodeQuality.ps1` for the
  library: 100% line, 100% branch, 0 failing members.
- Composition passes the flag in BL-2030.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. A kept LDAP seed (keepOnReset: true) now survives a directory reset; an unkept seed is still dropped.
