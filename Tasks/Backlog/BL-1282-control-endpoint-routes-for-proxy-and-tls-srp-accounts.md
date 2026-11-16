---
id: BL-1282
title: Control endpoint: routes for proxy and TLS-SRP accounts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1244, BL-1279]
touches: [Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests]
requirement: FR-061
created: 2026-11-16
completed:
---
# BL-1282 — Control endpoint: routes for proxy and TLS-SRP accounts

## Goal

A non-.NET test suite adds and removes proxy and TLS-SRP accounts over the loopback control endpoint.

## Context

ADR-0170 decision 4, after BL-1279. Shaped like `POST/DELETE /v1/accounts`.

## Acceptance criteria

- [ ] `POST` and `DELETE` on `/v1/proxy-accounts` and `/v1/tls-srp-accounts` call the matching `ComposedStores` members and answer as `/v1/accounts` does, including the refusal when the store is absent.
- [ ] The `store` event filter accepts the two new store names.
- [ ] The project keeps its quality gates.

## Notes

## Log

- 2026-11-16: Created.
