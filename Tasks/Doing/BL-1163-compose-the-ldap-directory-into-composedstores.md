---
id: BL-1163
title: Compose the LDAP directory into ComposedStores
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1158, BL-1162]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-14
completed:
---
# BL-1163 — Compose the LDAP directory into ComposedStores

## Goal

The composed instance's LDAP directory is registered in `ComposedStores`, so `ComposedServing.Stores` seeds it and a whole-instance reset, snapshot and restore include it.

## Context

- `Application/Surl.Composition.UnitLibrary/SurlServerComposition.cs`: `ComposeLdapDirectoryFile` and `LoadLdapServerAsync` build the `LdapProtocolServer` over its directory; `ServiceState` carries `ldapServer`. Register the directory through BL-1158's seam with BL-1162's operations, in the place BL-1151's ADR gives it, applying the ADR's rule for a directory loaded from `--directory`.
- `ComposedStores` gains the LDAP seed member the ADR names.

## Acceptance criteria

- [ ] Named tests: `ComposedStores` seeds an LDIF entry that an `ldap://` exchange of the same instance then finds in a search.
- [ ] Named tests: a whole-instance reset empties the in-memory directory; snapshot then restore brings the seeded entry back; a directory loaded from `--directory` behaves as the ADR decides (refused with `InvalidOperationException`, or reset to the loaded file's entries).
- [ ] Every existing `Surl.Composition.UnitTests` test passes unchanged; `dotnet build Surl.slnx -warnaserror` is clean and the fast tests are green.
- [ ] `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
