---
id: BL-1279
title: Compose the live coaps key lookup and the proxy and TLS-SRP account stores
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1244, BL-1274, BL-1275, BL-1276, BL-1277, BL-1278]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-16
completed:
---
# BL-1279 — Compose the live coaps key lookup and the proxy and TLS-SRP account stores

## Goal

`ComposedStores` controls the proxy and TLS-SRP accounts, and `coaps` reads the live account book per handshake.

## Context

ADR-0170 decisions 1 to 3. `DeferredCoapsProtocolServer` today copies `PreSharedKeyAccounts`; `ComposeProxyPolicy` builds a book `ComposedStores` does not hold; `ComposedTlsSrpAccounts` is read once. Depends on BL-1274 to BL-1278.

## Acceptance criteria

- [ ] The `coaps` lookup calls `AccountBook.TryFindPreSharedKey` per handshake, and passes the judge from BL-1278; an account added, removed, reset or restored changes who may log in over `coaps`.
- [ ] `ComposedStores` has `AddProxyAccount`, `RemoveProxyAccount`, `AddTlsSrpAccount`, `RemoveTlsSrpAccount`; both stores take part in reset, snapshot, restore and `StoreOrder`, each change published as a `StoreChangedEvent`.
- [ ] With no proxy account or no TLS-SRP configured the store is absent and an add is refused, never turning anonymous into authenticated.
- [ ] A seeded TLS-SRP add gives the same verifier each run.
- [ ] Composition keeps its quality gates.

## Notes

## Log

- 2026-11-16: Created.
