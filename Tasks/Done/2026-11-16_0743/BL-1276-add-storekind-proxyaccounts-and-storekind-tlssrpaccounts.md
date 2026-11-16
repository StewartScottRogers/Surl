---
id: BL-1276
title: Add StoreKind.ProxyAccounts and StoreKind.TlsSrpAccounts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1244]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Application/Surl.Mcp.UnitLibrary]
requirement: FR-061
created: 2026-11-16
completed: 2026-11-16
---
# BL-1276 — Add StoreKind.ProxyAccounts and StoreKind.TlsSrpAccounts

## Goal

`StoreKind` names the proxy account store and the TLS-SRP account store so events, snapshots and filters can address them.

## Context

ADR-0170 decisions 2 and 3. Members are appended; existing members keep their values and names.

## Acceptance criteria

- [x] `StoreKind` has `ProxyAccounts` and `TlsSrpAccounts` with XML docs, after the existing members.
- [x] Existing tests pinning the member list or names are updated and pass.
- [x] The Abstractions project keeps its quality gates.

## Notes

- Added `Application/Surl.Mcp.UnitLibrary` to `touches`: `EventToolFilter.EventProperties` hard-codes the `store` schema enum, and `StoreEventFilterTests.Schema_StoreEnum_ListsEveryStoreKindInSnakeCase` pins it to every `StoreKind` member - the test that failed lane 2's integration. It now lists `proxy_accounts` and `tls_srp_accounts`. No task in Doing named that project.
- The enum code is lane 2's commit 525581ad1, reapplied unchanged.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. Lane 2 could not integrate: fast tests failed twice (Schema_StoreEnum_ListsEveryStoreKindInSnakeCase; then Schema_StoreEnum_ListsEveryStoreKindInSnakeCase) after rebasing onto the other lanes' work. The work is on branch factory/BL-1276-lane-2-20261116-063259; start with git cherry-pick --no-commit factory/BL-1276-lane-2-20261116-063259 and fix it.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. StoreKind has ProxyAccounts and TlsSrpAccounts; the MCP get_events store enum lists them; build clean, fast tests green
