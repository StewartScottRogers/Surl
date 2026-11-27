---
id: BL-1894
title: Amend ADR-0158, ADR-0159 and ADR-0167 to describe the store registration seam the code has, not IControllableStore
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0158-how-the-redis-keyspace-the-ldap-directory-and-the-dns-zones-are-seeded-reset-snapshotted-restored-and-observed.md, Documentation/Planning/Decisions/ADR-0159-which-broker-held-state-a-reset-snapshot-and-restore-of-the-message-bus-cover.md, Documentation/Planning/Decisions/ADR-0167-what-a-reset-snapshot-and-restore-do-with-in-progress-object-uploads-and-the-oci-and-mcp-servers-tokens.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1894 — Amend ADR-0158, ADR-0159 and ADR-0167 to describe the store registration seam the code has, not IControllableStore

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

- [ ] Each of the three ADRs has a dated amendment, named in its header and "Decided by Claude under Stewart's
      delegation", saying how a store joins `ComposedStores` in the code, and that this supersedes its
      `IControllableStore` text.
- [ ] The ADR-0158 row of the decisions index no longer says stores join through `IControllableStore`.
- [ ] No original decision sentence is rewritten in the ADRs, and no `.cs` file changes.

## Notes

## Log

- 2026-11-27: Created.
