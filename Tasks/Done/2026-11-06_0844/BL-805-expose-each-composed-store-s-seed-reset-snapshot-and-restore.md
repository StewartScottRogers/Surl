---
id: BL-805
title: Expose each composed store's seed, reset, snapshot and restore through ComposedServing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-803, BL-799, BL-800, BL-801, BL-802]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-805 — Expose each composed store's seed, reset, snapshot and restore through ComposedServing

## Goal

A host holding a `ComposedServing` can seed, reset, snapshot and restore that instance's mail store, content
store, message bus and account set - one store at a time or all together - so `Surl.Api`, `Surl.Mcp` and the
control endpoint all drive the same code.

## Context

- Decision: BL-822's ADR decision 3 (the whole-instance snapshot - planning name `ComposedSnapshot` holding one
  snapshot per store - and reset's order across stores; what happens with `--directory`).
- Built on: BL-799 (mail), BL-800 (content), BL-801 (bus), BL-802 (accounts), BL-803 (the instance's stream).
- Where: `Application/Surl.Composition.UnitLibrary` (`ComposedServing.cs`, `ServiceState.cs`). Expose a
  store-control surface (planning name `ComposedStores`) on `ComposedServing`; keep these types `public` but
  outside `Surl.Api`'s supported surface, as the library's `CLAUDE.md` says. Update that `CLAUDE.md`.
- Reset and restore of all stores is one operation per store in the fixed load order of ADR-0100 decision 1
  (content, mail, bus, then accounts as the ADR places them), so a failure is reported deterministically.
- Stand-in: controllable; the cross-protocol proof is in Acceptance.

## Acceptance criteria

- [x] Named tests over `FakeListenerFactory` (no network): a message seeded through `ComposedServing` is fetched
      over a fake IMAP connection; a file seeded is read over a fake HTTP connection; reset empties both and keeps
      the configured mailboxes and shares; snapshot, change, restore of the whole instance gives back every store's
      state; each operation's events appear on the instance's stream.
- [x] A named test shows the `--directory` behaviour the ADR decides.
- [x] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      the library keeps 100% line and branch coverage.

## Notes

- Delivered: `ComposedStores` (on `ComposedServing.Stores`), `ComposedSnapshot`, `ComposedStoreException` and the
  internal `TakenSnapshot` in `Surl.Composition.UnitLibrary`; the start builds it from the content store, the
  service state and `authentication.Policy.Settings.Accounts`. Tests: `ComposedStoresTests` (11), every new type
  at 100% line and branch.
- Choice: each store's reset and snapshot sit in one ordered table (`StoreControl`), and a snapshot carries one
  restore closure per store, so there is no `StoreKind` switch to grow and no unreachable arm; one store at a
  time is `ResetAsync(StoreKind)`/`TakeSnapshotAsync(StoreKind)` (an unknown kind is
  `ArgumentOutOfRangeException`), and `RestoreAsync` restores whatever the snapshot holds.
- Choice: a refusal is a `ComposedStoreException : InvalidOperationException` with `Store` and the store's own
  refusal as `InnerException`, so a host maps it like the store's own (409 on the control endpoint) and the
  failing store is named deterministically. The planning name `ComposedSnapshot` kept; it carries its instance
  (`ArgumentException` for another's), and `MaxSnapshotBytes` (default 256 MiB) is checked after each store,
  the bus being handed what is left as its own bound.
- Choice: `SeedMailAsync`/`SeedPublishAsync`/`SeedEnqueueAsync` save the store after seeding (a no-op in
  memory), so a seed into a store on disk is persisted as a protocol's write is; a bus save failure, which the
  bus reports as a result, is thrown as `IOException` so a seed never fails silently.
- Fix found on the way: the account book was built without the instance's event stream, so account changes
  were never published; `AuthenticationComposition.ComposeSettings` gained an overload taking the publisher.
- `--directory` (ADR-0124 decision 3): the composition tests' disk is an in-memory file system, so the content
  store refuses through its object metadata index's on-disk path; mail and bus refuse as on disk; a whole reset
  or snapshot stops at `Content`; seeding a file works and accounts stay controllable
  (`Stores_WithDirectory_RefuseResetSnapshotAndRestoreOfTheDiskStoresButSeedThemAndControlAccounts`).
- "Configured mailboxes and shares": `Surl.Composition` composes no `--mailbox` folders or shares yet, so the
  reset test shows the configured state as it exists today - the INBOX still selectable (empty) and a folder
  the host supplied at start kept.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ComposedServing.Stores seeds, resets, snapshots and restores the content, mail, bus and account stores, one or all, in ADR-0124 order
