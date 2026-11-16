---
id: BL-1267
title: Publish decrypted bytes from the engine: set ExchangeContext.DecryptedBytes, publish layered ExchangeBytesEvents and trace them (ADR-0169)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1266]
touches: [Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: FR-060
created: 2026-11-16
completed: 2026-11-16
---
# BL-1267 — Publish decrypted bytes from the engine

## Goal

An exchange's `DecryptedBytes` sink publishes `ExchangeBytesEvent`s with the layer and stream identifier and writes them to the exchange log for `--trace`, while wire counts and bytes stay as they are.

## Context

ADR-0169 decisions 1, 3, 4. `ServingEngine.OpenExchange` builds the context; `EventPublishingExchangeLog` already publishes wire bytes and counts them for `ExchangeEndedEvent`.

## Acceptance criteria

- [x] `OpenExchange` sets `DecryptedBytes` to a sink publishing `ExchangeBytesEvent` with `Layer` and `StreamId`, only while `IsPublishingBytes` is true (no copy otherwise).
- [x] A test: a server calling the sink gets events with the layer, stream, direction and a copy of the bytes.
- [x] A test: `ExchangeEndedEvent` byte counts include wire bytes only.
- [x] The trace log receives a record naming the layer (and `stream <id>`) for each chunk; wire records are unchanged (test).
- [x] 100% line and branch coverage kept; `dotnet build` clean.

## Notes

- Added `EventPublishingDecryptedBytesSink` (Core): notes each chunk to the exchange log as `<Layer> bytes <direction>[, stream <id>], N bytes: <hex>` (the log interface has only `Note` for non-wire records) and publishes the event only while `IsPublishingBytes`. Wire counts come from `EventPublishingExchangeLog` alone.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Engine sets DecryptedBytes sink; tests green
