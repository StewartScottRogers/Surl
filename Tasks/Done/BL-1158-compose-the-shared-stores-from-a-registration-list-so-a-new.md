---
id: BL-1158
title: Compose the shared stores from a registration list so a new store joins reset, snapshot and restore by one entry
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1151]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-14
completed: 2026-11-14
---
# BL-1158 — Compose the shared stores from a registration list so a new store joins reset, snapshot and restore by one entry

## Goal

`ComposedStores` resets, snapshots and restores an ordered list of store registrations instead of four hard-coded stores, so the Redis keyspace, LDAP directory, DNS zones and broker state (BL-1161, BL-1163, BL-1165, BL-1170, BL-1171) each join by adding one entry - with no change to what the four existing stores do.

## Context

- `Application/Surl.Composition.UnitLibrary/ComposedStores.cs`: `StoreOrder` is a fixed `[Content, Mail, Bus, Accounts]`, and the private `StoreControl(StoreKind, ResetAsync, TakeAsync)` record and `TakeContentSnapshotAsync`/`TakeMailSnapshotAsync`/... methods are written per store. `ComposedSnapshot.cs` and `TakenSnapshot.cs` hold the result. ADR-0124 decision 3 fixes the order (content, mail, bus, accounts), the first failure stopping the run and naming the store, `MaxSnapshotBytes` across all stores, and the disk-store refusal.
- Build the seam BL-1151's ADR decides (for example a public `ComposedStoreRegistration` with the kind, reset, snapshot and restore delegates and an "is on disk" check), make the four existing stores the first four entries in today's order, and keep `ComposedStores`' public members and their behaviour unchanged.
- "Open for extension" principle (root `CLAUDE.md`): explicit, AOT-safe registration; no reflection.

## Acceptance criteria

- [x] `ComposedStores` iterates its registrations for `ResetAsync`, `TakeSnapshotAsync` and `RestoreAsync`; the four existing stores are registrations in ADR-0124 decision 3's order, and `StoreOrder` reports the registered order.
- [x] Every existing test in `Application/Surl.Composition.UnitTests` passes unchanged (the store-control, snapshot-bound, disk-refusal and order tests included).
- [x] A new test registers a fifth fake store and shows it is reset, snapshotted (counted toward `MaxSnapshotBytes`) and restored after the four, and that its failure stops the run naming it.
- [x] `dotnet build Surl.slnx -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage with no method over complexity 10.

## Notes

- Seam: internal `ComposedStoreRegistration(Store, ResetAsync, TakeSnapshotAsync)`; the snapshot's `TakenSnapshot` carries its own restore, so no third delegate. The disk refusal stays each store's own `InvalidOperationException`, so no "is on disk" check is needed.
- `ComposedStores`' constructor takes optional `additionalRegistrations`, appended after the four. The static `StoreOrder` stays the standard four-store order (existing tests use it); the new instance `RegisteredStoreOrder` reports the actual registered order, extras included.
- The test's fifth store uses `(StoreKind)100`; BL-1161 and later add real enum members.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. ComposedStores resets, snapshots and restores a registration list; a fifth store joins by one entry
