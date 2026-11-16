---
id: BL-1282
title: Control endpoint: routes for proxy and TLS-SRP accounts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1244, BL-1279]
touches: [Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-16
completed: 2026-11-16
---
# BL-1282 — Control endpoint: routes for proxy and TLS-SRP accounts

## Goal

A non-.NET test suite adds and removes proxy and TLS-SRP accounts over the loopback control endpoint.

## Context

ADR-0170 decision 4, after BL-1279. Shaped like `POST/DELETE /v1/accounts`.

## Acceptance criteria

- [x] `POST` and `DELETE` on `/v1/proxy-accounts` and `/v1/tls-srp-accounts` call the matching `ComposedStores` members and answer as `/v1/accounts` does, including the refusal when the store is absent.
- [x] The `store` event filter accepts the two new store names.
- [x] The project keeps its quality gates.

## Notes

- `touches` widened to `Surl.Composition.UnitLibrary`/`.UnitTests`: `IControlTarget` gained four members, so `ComposedControlTarget` had to implement them; no task in Doing touches Composition.
- Add replaces a same-named account (remove then add), as `/v1/accounts` does. An absent store (`InvalidOperationException`) is `409 conflict` with the store's message; an unknown user on DELETE is `404`. The `store` filter already enumerates `StoreKind`, so the new names needed only tests.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. POST/DELETE /v1/proxy-accounts and /v1/tls-srp-accounts drive the account stores; store filter takes both names
