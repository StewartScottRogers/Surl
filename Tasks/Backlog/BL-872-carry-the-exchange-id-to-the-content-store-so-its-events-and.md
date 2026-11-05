---
id: BL-872
title: Carry the exchange ID to the content store so its events and fault points name the exchange
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-789]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-872 — Carry the exchange ID to the content store so its events and fault points name the exchange

## Goal

A content-store `StoreChangedEvent`, `FaultAppliedEvent` and `FaultPoint` carry the exchange that made
the change, so a test can tie an upload's store change to the FTP or HTTP exchange that sent it.

## Context

- ADR-0129 decision 2: `ExchangeId` is `null` on every content change because the store's API takes a
  `ContentPathMapping` built from a `ContentCaller`, which carries an account and a scheme but no exchange.
- Planned shape: `ContentCaller` gains an optional exchange ID (for example `ForAccount(name, scheme)`
  and `Anonymous(scheme)` with a `WithExchange(long)` or an `init` property), and
  `ContentStoreEventsAndFaults` puts `mapping.Caller.ExchangeId` on the event and the fault point.
  Record the choice as an amendment to ADR-0129.
- Each file server then passes `ExchangeContext.ExchangeId` when it builds its caller; file one task per
  server family for that wiring, depending on this one.

## Acceptance criteria

- [ ] Named `ContentStoreTests` show a write, delete, rename, folder change and out-of-space refusal made
      through a caller with exchange ID 7 publish events with `ExchangeId` 7 and ask the plan with
      `ExchangeId` 7; a caller with none still publishes `null`.
- [ ] `dotnet build` is clean, the fast tests are green, and `Surl.Content.UnitLibrary` keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
