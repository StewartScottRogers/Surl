---
id: BL-1848
title: Rename the store registration combiner and its caller so their names and comments cover sessions as well as uploads and brokers
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1848 — Rename the store registration combiner and its caller so their names and comments cover sessions as well as uploads and brokers

## Goal

The class that joins same-kind store registrations, the method that builds the exchange, producer and session
entries and the `ComposedServing.Stores` summary name what they cover.

## Context

Found while running BL-1831 (checked against the code on 2026-11-27). Renames and comments only; behaviour,
store order and published events do not change. All in `Application/Surl.Composition.UnitLibrary`.

- `UploadStoreRegistrations` (`UploadStoreRegistrations.cs`): the name and class summary say uploads, but its
  `CombineOf(StoreKind kind, ...)` also joins the MQTT 5, OCI token and MCP entries into the one
  `StoreKind.Sessions` entry. Rename the class and file to `SameKindStoreRegistrations`, with one method
  `Combine(StoreKind kind, params ComposedStoreRegistration?[] registrations)`; the uploads-only
  `Combine(params ...)` overload goes, and its caller passes `StoreKind.Uploads`.
- Callers: `SurlServerComposition.Start.cs` (`ControlledUploads` and `ControlledBrokerStores`) and
  `Application/Surl.Composition.UnitTests/AzureStagedUploadStoreTests.cs`.
- `ControlledBrokerStores` and its comment ("The stores the composed brokers hold beside the bus ... in ordinal
  order of scheme"): its `Sessions` entry also holds the OCI registry's tokens and the MCP server's state, which
  are no broker's, and runs them in the order MQTT 5, OCI, MCP, which is not ordinal. Rename it
  `ControlledBrokerAndSessionStores`. The entries are AMQP 0-9-1 exchanges, Kafka producers, then sessions.
- `ComposedServing.cs`, `Stores` summary: "The instance's stores - content, mail, message bus and accounts". An
  instance registers up to 13 (`ComposedStores.RegisteredStoreOrder`).
- The library's `CLAUDE.md` names `UploadStoreRegistrations.Combine` and `CombineOf` in the Azure Storage,
  Google Cloud Storage and OCI bullets; update those names.
- BL-1830 corrects `ComposedStores.cs`, `ComposedSnapshot.cs`, `Amqp0ExchangeStoreRegistration.cs` and the
  `CLAUDE.md` bullets on store control and DNS zones; leave those statements to it.

## Acceptance criteria

- [x] `SameKindStoreRegistrations.Combine(StoreKind, ...)` replaces `UploadStoreRegistrations.Combine` and
      `CombineOf`, its summary names uploads and sessions as what it joins, and neither old name appears in any
      `.cs` file or in the library's `CLAUDE.md`.
- [x] `ControlledBrokerStores` is `ControlledBrokerAndSessionStores`, and its comment names the OCI tokens and MCP
      state, with the order the code runs, without saying ordinal order of scheme.
- [x] The `ComposedServing.Stores` summary says the instance's stores are every registered one and does not list
      only content, mail, message bus and accounts.
- [x] The existing `Surl.Composition.UnitTests` tests pass with no `TestCategory=Integration`, changed only for
      the new names.
- [x] `dotnet build Application/Surl.Composition.UnitTests -warnaserror` is clean.

## Notes

- Delivered by `align-and-document`: renames and comments only; store order and published events unchanged.
- Already true before this run: the `ComposedServing.Stores` summary named every registered store, and the
  renamed method's comment named the OCI tokens and MCP state with no "ordinal order of scheme" wording.
- Also corrected in the `Stores` summary: it implied every store can be seeded; seeding exists only for those
  `ComposedStores` has a seed or add method for.
- BL-1857 (Backlog) still names `ControlledBrokerStores` in its Context; it is now
  `ControlledBrokerAndSessionStores`. Archived tasks keep the old names.
- Left as is: the two `Combine_*` tests stay in `AzureStagedUploadStoreTests`, since this task changes tests only
  for the new names.
- Verify: build 0 warnings, `dotnet format --verify-no-changes` clean, fast tests of the whole solution green
  (`Surl.Composition.UnitTests` 935 passed). No integration-level code changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The store registration combiner is SameKindStoreRegistrations.Combine and its caller ControlledBrokerAndSessionStores; names and comments cover uploads, brokers and sessions
