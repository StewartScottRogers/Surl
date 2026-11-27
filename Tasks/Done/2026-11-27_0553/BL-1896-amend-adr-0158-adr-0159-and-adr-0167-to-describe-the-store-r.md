---
id: BL-1896
title: Amend ADR-0158, ADR-0159 and ADR-0167 to describe the store registration seam the code has, not IControllableStore
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0158-how-the-redis-keyspace-the-ldap-directory-and-the-dns-zones-are-seeded-reset-snapshotted-restored-and-observed.md, Documentation/Planning/Decisions/ADR-0159-which-broker-held-state-a-reset-snapshot-and-restore-of-the-message-bus-cover.md, Documentation/Planning/Decisions/ADR-0167-what-a-reset-snapshot-and-restore-do-with-in-progress-object-uploads-and-the-oci-and-mcp-servers-tokens.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1896 — Amend ADR-0158, ADR-0159 and ADR-0167 to describe the store registration seam the code has, not IControllableStore

## Goal

ADR-0158, ADR-0159 and ADR-0167, and the decisions index, describe by dated amendment the seam a store joins
`ComposedStores` by, and none presents `IControllableStore` or `ProtocolServerRegistration.ControlledStore` as
existing.

## Context

Found while running BL-1857 (2026-11-27): ADR-0158 decision 8, ADR-0159 decision 3, ADR-0167 decision 3 and the
ADR-0158 row of `Documentation/Planning/Decisions/README.md` name an `IControllableStore` seam that no `.cs` file
declares. In `Application/Surl.Composition.UnitLibrary`, a store joins through a `ComposedStoreRegistration`
built by a `*StoreRegistration` class (for example `Amqp0ExchangeStoreRegistration`), listed in `ComposeStores`,
`ControlledBrokerAndSessionStores` and `ControlledUploads` in `SurlServerComposition.Start.cs`;
`SameKindStoreRegistrations` combines several of one `StoreKind`. The code stands and the records are amended to
it (root `CLAUDE.md`, "Decisions"). Check `ProtocolServerRegistration` for the member before writing. No code or
behaviour changes.

## Acceptance criteria

- [x] Each of the three ADRs has a dated amendment, named in its header and "Decided by Claude under Stewart's
      delegation", saying how a store joins `ComposedStores` in the code, and that this supersedes its
      `IControllableStore` text.
- [x] The ADR-0158 row of the decisions index no longer says stores join through `IControllableStore`.
- [x] No original decision sentence is rewritten in the ADRs, and no `.cs` file changes.

## Notes

- The full description is in ADR-0158 Amendment 3; ADR-0159 Amendment 2 and ADR-0167 Amendment 2 link to it and give
  only their own classes, so the seam is described once. Why: three copies would drift apart.
- Each superseded decision gained an italic pointer to its amendment, the convention BL-1857 used; no original
  sentence changed.
- The amendments also record what else differed from the code: `ComposedStores` has `RegisteredStoreOrder`, not
  `Order`; no broker registers from `ExtendedProtocolServers`; and a typed seed or read method still means editing
  `ComposedStores`.
- `ProtocolServerRegistration` has only `Schemes`, `Create` and `MakesLogin`: no `ControlledStore`.
- Gates: `dotnet build` 0 warnings, 0 errors; fast tests passed. No `.cs` file changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. ADR-0158, ADR-0159, ADR-0167 and the index describe the ComposedStoreRegistration seam the code has
