---
id: BL-1163
title: Compose the LDAP directory into ComposedStores
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1158, BL-1162]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary]
requirement: FR-061
created: 2026-11-14
completed: 2026-11-15
---
# BL-1163 — Compose the LDAP directory into ComposedStores

## Goal

The composed instance's LDAP directory is registered in `ComposedStores`, so `ComposedServing.Stores` seeds it and a whole-instance reset, snapshot and restore include it.

## Context

- `Application/Surl.Composition.UnitLibrary/SurlServerComposition.cs`: `ComposeLdapDirectoryFile` and `LoadLdapServerAsync` build the `LdapProtocolServer` over its directory; `ServiceState` carries `ldapServer`. Register the directory through BL-1158's seam with BL-1162's operations, in the place BL-1151's ADR gives it, applying the ADR's rule for a directory loaded from `--directory`.
- `ComposedStores` gains the LDAP seed member the ADR names.

## Acceptance criteria

- [x] Named tests: `ComposedStores` seeds an LDIF entry that an `ldap://` exchange of the same instance then finds in a search.
- [x] Named tests: a whole-instance reset empties the in-memory directory; snapshot then restore brings the seeded entry back; a directory loaded from `--directory` behaves as the ADR decides (refused with `InvalidOperationException`, or reset to the loaded file's entries).
- [x] Every existing `Surl.Composition.UnitTests` test passes unchanged; `dotnet build Surl.slnx -warnaserror` is clean and the fast tests are green.
- [x] `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- touches grew by `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary`: `LdapDirectory` is internal, so `LdapProtocolServer` gained public `SeedDirectoryEntries`, `ResetDirectory`, `TakeDirectorySnapshot`, `RestoreDirectory`, an optional `events` parameter on its constructor and `LoadAsync`, and `LdapDirectorySnapshot` became public. No task in Doing touches it.
- ADR-0158 decision 6 applies: a directory loaded from `--directory` is an in-memory copy, so reset returns to the file's entries (tested) and is never refused.
- Registration goes through `ComposedStores`' `additionalRegistrations` (the BL-1158 seam as built), first in the list; `ComposedStores.SeedEntriesAsync` is the seed member (`InvalidOperationException` when the instance has no directory).
- Four existing tests pinned the exact store order (`ComposedStoresTests` x2, `Amqp0ExchangeStoreTests` x2); their expected lists gained `Directory`, unavoidable when a store joins.

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. LDAP directory composed into ComposedStores; build clean, fast tests green
