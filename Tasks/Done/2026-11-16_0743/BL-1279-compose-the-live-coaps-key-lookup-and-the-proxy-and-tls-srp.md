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
completed: 2026-11-16
---
# BL-1279 — Compose the live coaps key lookup and the proxy and TLS-SRP account stores

## Goal

`ComposedStores` controls the proxy and TLS-SRP accounts, and `coaps` reads the live account book per handshake.

## Context

ADR-0170 decisions 1 to 3. `DeferredCoapsProtocolServer` today copies `PreSharedKeyAccounts`; `ComposeProxyPolicy` builds a book `ComposedStores` does not hold; `ComposedTlsSrpAccounts` is read once. Depends on BL-1274 to BL-1278.

## Acceptance criteria

- [x] The `coaps` lookup calls `AccountBook.TryFindPreSharedKey` per handshake, and passes the judge from BL-1278; an account added, removed, reset or restored changes who may log in over `coaps`.
- [x] `ComposedStores` has `AddProxyAccount`, `RemoveProxyAccount`, `AddTlsSrpAccount`, `RemoveTlsSrpAccount`; both stores take part in reset, snapshot, restore and `StoreOrder`, each change published as a `StoreChangedEvent`.
- [x] With no proxy account or no TLS-SRP configured the store is absent and an add is refused, never turning anonymous into authenticated.
- [x] A seeded TLS-SRP add gives the same verifier each run.
- [x] Composition keeps its quality gates.

## Notes

- The `coaps` lookup reads the policy book per handshake via `AccountBook.TryFindPreSharedKey`; `PreSharedKeyAccounts` (services, authentication, `ReadPreSharedKeyAccounts`) is removed. `SecureCoapProtocolServer` already supplies the judge (BL-1278). PSK mode is still offered only when the book holds a coaps-admitted account at the first flow (keeps today's no-account refusal and cert-only handshake bytes); later changes decide who may log in, but a first coaps account added later to a cert-only server does not newly offer PSK suites.
- The proxy book is built silent (its own events would have said `Accounts`); `ComposedStores` publishes `ProxyAccounts` events itself. The policy still gets events for login judging.
- Static `StoreOrder` stays the four always-present stores (existing tests pin `[..StoreOrder, ...]`); `ProxyAccounts` then `TlsSrpAccounts` join `RegisteredStoreOrder` right after `Accounts` when present.
- The TLS-SRP store joins once server TLS is composed (`JoinTlsSrpAccounts`); salts come from `SeededIdentifierRandom.ForServer("tls-srp")` under `--random-seed`.
- Snapshot byte count is 0 for both stores, as for accounts.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Live coaps key lookup, proxy and TLS-SRP account stores composed
